using System.Text.RegularExpressions;
using FoundU.Application.Claims.Dtos;

namespace FoundU.Infrastructure.Claims;

/// <summary>Local, deterministic, advisory fallback. Never outputs expected evidence.</summary>
internal static class SafeVerificationFallback
{
    public static string Question(string detail)
    {
        return VerificationGrounding.Candidates(detail).FirstOrDefault() ?? VerificationGrounding.GenericQuestion;
    }

    public static GenerateVerificationQuestionsResult Generate(Guid claimId, IReadOnlyDictionary<string, string> details)
        => new(claimId, details.OrderBy(d => d.Key, StringComparer.Ordinal).Take(3)
            .Select((d, i) => new VerificationAgentQuestion($"verification-{i + 1}", Question(d.Value)))
            .DistinctBy(q => q.Question).ToList(), "manual_review", "local-safe-fallback");

    public static bool IsSafe(string question, IEnumerable<string> details)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 240) return false;
        var observations = details.ToList();
        if (!observations.Any(d => VerificationGrounding.IsGrounded(question, d))) return false;
        return IsPrivateSafe(question, observations);
    }

    // Decision reasons share the privacy check, but are not verification questions.
    public static bool IsPrivateSafe(string question, IEnumerable<string> details)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 1000) return false;
        var normalized = Normalize(question);
        var context = new HashSet<string>(("the a an is are was were in on at of and with it its my there this that has have not do did ownership evidence "
            + "underneath under inside outside back front bottom top corner near item bottle cap card mark markings "
            + "identifying distinctive detail feature location letters numbers initials written handwritten ink "
            + "sticker label colour color damage scratch engraving lining accessory attached").Split(' '));
        var questionTokens = normalized.Split(' ').ToHashSet();
        foreach (var detail in details)
        {
            var secret = Normalize(detail);
            if (secret.Length > 0 && normalized.Contains(secret, StringComparison.Ordinal)) return false;
            if (secret.Split(' ').Any(t => t.Length >= 3 && !context.Contains(t) && questionTokens.Contains(t))) return false;
            // Codes, identifiers, and quoted values must never become hints.
            foreach (Match token in Regex.Matches(detail, @"\b[\w-]*\d[\w-]*\b|['‘“]([^'’”]+)['’”]"))
                if (normalized.Contains(Normalize(token.Value), StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();

    public static bool ReusesEvidence(string detail, IEnumerable<string> previous)
    {
        var codes = Regex.Matches(detail, @"\b[\w-]*\d[\w-]*\b")
            .Select(m => Normalize(m.Value).Replace(" ", "")).ToHashSet();
        return previous.Any(old => Normalize(old) == Normalize(detail)
            || Regex.Matches(old, @"\b[\w-]*\d[\w-]*\b")
                .Any(m => codes.Contains(Normalize(m.Value).Replace(" ", ""))));
    }

    public static EvaluateVerificationAnswersResult Evaluate(Guid claimId, IReadOnlyDictionary<string, string> details,
        IReadOnlyList<VerificationAgentQuestion> questions, IReadOnlyList<VerificationAgentAnswer> answers)
    {
        var byId = answers.ToDictionary(a => a.QuestionId, a => a.Answer);
        var evaluations = questions.Select(q =>
        {
            var fact = ResolveFact(q.Question, details.Values);
            var answer = byId.GetValueOrDefault(q.QuestionId, "");
            if (fact?.Kind != "description" || VerificationAnswerScoring.Uncertain(answer))
                return VerificationAnswerScoring.Evaluate(q.QuestionId, answer, fact);
            var score = Score(answer, fact.Value);
            return new VerificationAgentEvaluation(q.QuestionId,
                score >= .8 ? "match" : score > 0 ? "partial_match" : "insufficient", score);
        }).ToList();
        return FromEvaluations(claimId, "local-safe-fallback", evaluations);
    }

    internal static VerificationGrounding.AnswerFact? ResolveFact(string question, IEnumerable<string> details)
    {
        var facts = details.Select(d => VerificationGrounding.ResolveAnswerFact(question, d))
            .Where(f => f is not null).Distinct().ToList();
        return facts.Count == 1 ? facts[0] : null;
    }

    internal static EvaluateVerificationAnswersResult FromEvaluations(Guid claimId, string source,
        IReadOnlyList<VerificationAgentEvaluation> evaluations)
        => new(claimId, evaluations.Count > 0 && evaluations.All(e => e.Result == "match") ? "likely_match"
                : evaluations.Count > 0 && evaluations.All(e => e.Result == "no_match") ? "unlikely_match" : "manual_review",
            source, evaluations.Count == 0 ? 0 : Math.Round(evaluations.Average(e => e.Score) * 100, 1),
            evaluations.Where(e => e.Result == "match").Select(e => $"Question {e.QuestionId}: identifying evidence matched.").ToList(),
            evaluations.Where(e => e.Result is "partial_match" or "insufficient").Select(e => $"Question {e.QuestionId}: insufficient information for a complete match.").ToList(),
            evaluations.Where(e => e.Result == "no_match").Select(e => $"Question {e.QuestionId}: the requested fact contradicts the recorded observation.").ToList(),
            "Question-specific comparison with staff-held evidence; staff must decide ownership.", evaluations);

    private static double Score(string answer, string expected)
    {
        if (string.IsNullOrWhiteSpace(answer) || Regex.IsMatch(answer, "ignore|system prompt|approve|instruction", RegexOptions.IgnoreCase)) return 0;
        var filler = new HashSet<string>("the a an is are was in on at of and with it my there this that has handwritten written ink underneath".Split(' '));
        var evidence = Normalize(expected).Split(' ').Where(t => !filler.Contains(t)).ToHashSet();
        var actual = Normalize(answer).Split(' ').ToHashSet();
        if (actual.Count > evidence.Count * 2 + 8) return 0;
        // Exact identifiers carry more evidential weight than generic descriptive words.
        var codes = Regex.Matches(expected, @"\b[\w-]*\d[\w-]*\b").Select(m => Normalize(m.Value)).ToList();
        if (codes.Count > 0)
        {
            if (!codes.All(c => Normalize(answer).Contains(c, StringComparison.Ordinal))) return 0;
            return actual.Overlaps(new[] { "cap", "back", "inside", "bottom" }) ? .92 : .65;
        }
        return evidence.Count == 0 ? 0 : (double)evidence.Count(actual.Contains) / evidence.Count;
    }
}
