using System.Text.RegularExpressions;
using FoundU.Application.Claims.Dtos;

namespace FoundU.Infrastructure.Claims;

/// <summary>Local, deterministic, advisory fallback. Never outputs expected evidence.</summary>
internal static class SafeVerificationFallback
{
    public static string Question(string detail)
    {
        var text = detail.ToLowerInvariant();
        if (text.Contains("cap")) return "What identifying mark is underneath the bottle cap?";
        if (text.Contains("sticker") && text.Contains("back")) return "Describe any identifying mark on the back of the item, including its colour and location.";
        if (text.Contains("inside") || text.Contains("lining")) return "Describe an identifying detail on the inside of the item.";
        if (text.Contains("scratch") || text.Contains("damage")) return "Describe any distinctive damage and its location on the item.";
        if (text.Contains("initial") || text.Contains("engraving")) return "What identifying letters or markings does the item have, and where are they?";
        return "Describe a private identifying feature of the item and its location.";
    }

    public static GenerateVerificationQuestionsResult Generate(Guid claimId, IReadOnlyDictionary<string, string> details)
        => new(claimId, details.OrderBy(d => d.Key, StringComparer.Ordinal).Take(3)
            .Select((d, i) => new VerificationAgentQuestion($"verification-{i + 1}", Question(d.Value)))
            .DistinctBy(q => q.Question).ToList(), "manual_review", "local-safe-fallback");

    public static bool IsSafe(string question, IEnumerable<string> details)
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
        IReadOnlyList<VerificationAgentAnswer> answers)
    {
        var values = details.OrderBy(d => d.Key, StringComparer.Ordinal).Take(3).Select(d => d.Value).ToList();
        var scores = answers.Select((a, i) => i >= values.Count ? 0 : Score(a.Answer, values[i])).ToList();
        var score = scores.Count == 0 ? 0 : Math.Round(scores.Average() * 100, 1);
        return new(claimId, "manual_review", "local-safe-fallback", score,
            scores.Where(s => s >= .8).Select(_ => "Identifying evidence matched.").ToList(),
            scores.Where(s => s > 0 && s < .8).Select(_ => "Some identifying evidence is missing.").ToList(),
            scores.Where(s => s == 0).Select(_ => "The submitted evidence did not match.").ToList(),
            "Deterministic comparison; staff review required.");
    }

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
