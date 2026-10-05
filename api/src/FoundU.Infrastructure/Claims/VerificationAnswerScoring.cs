using System.Text.RegularExpressions;
using FoundU.Application.Claims.Dtos;

namespace FoundU.Infrastructure.Claims;

/// <summary>Deterministic typed attribute checks. Expected values are never returned.</summary>
internal static class VerificationAnswerScoring
{
    private static readonly HashSet<string> Colors = "black white red blue green yellow orange purple pink gray brown silver gold".Split(' ').ToHashSet();
    private static readonly HashSet<string> Wrapper = ("the a an it its has have had is are was were i my item color colour brand located "
        + "location of found this that type there any").Split(' ').ToHashSet();

    internal static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant().Replace("grey", "gray"), @"[^a-z0-9]+", " ").Trim();
    internal static bool Uncertain(string value) => string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.ToLowerInvariant(),
        @"\b(?:don['’]?t know|do not know|no idea|unknown|unsure|not sure|maybe|possibly|might|could|guess|cannot remember|can['’]?t remember)\b");

    internal static bool IsUsableResult(EvaluateVerificationAnswersResult result, IReadOnlyList<VerificationAgentQuestion> questions)
    {
        if (result.Score is not { } score || !double.IsFinite(score) || score is < 0 or > 100) return false;
        if (result.Evaluations is not { } evaluations) return false;
        if (evaluations.Count != questions.Count || evaluations.Select(e => e.QuestionId).Distinct().Count() != questions.Count
            || questions.Any(q => !evaluations.Any(e => e.QuestionId == q.QuestionId))) return false;
        if (evaluations.Any(e => !double.IsFinite(e.Score) || e.Score is < 0 or > 1 || e.Result switch
            {
                "match" => e.Score < .8,
                "partial_match" => e.Score is <= 0 or >= .8,
                "no_match" => e.Score >= .4,
                "insufficient" => e.Score != 0,
                _ => true,
            })) return false;
        if (Math.Abs(score - Math.Round(evaluations.Average(e => e.Score) * 100, 1)) >= .001) return false;
        if (evaluations.All(e => e.Result == "match") && (result.ConflictingInformation?.Count > 0 || result.MissingInformation?.Count > 0)) return false;
        if (evaluations.All(e => e.Result != "match") && result.MatchedEvidence?.Count > 0) return false;
        return true;
    }

    public static VerificationAgentEvaluation Evaluate(string id, string answer, VerificationGrounding.AnswerFact? fact)
    {
        VerificationAgentEvaluation Result(string result, double score) => new(id, result, score);
        if (fact is null || Uncertain(answer) || Regex.IsMatch(answer, "ignore|system prompt|approve|instruction", RegexOptions.IgnoreCase))
            return Result("insufficient", 0);
        var actual = Normalize(answer).Split(' ').ToHashSet();
        var expected = Normalize(fact.Value).Split(' ').Except(new[] { "the", "a", "an" }).ToHashSet();
        var wrapper = Wrapper.Append(fact.Subject).ToHashSet();
        if (fact.Subject == "cap") wrapper.Add("bottle");
        if (actual.Overlaps(new[] { "not", "no", "isn", "wasn", "doesn", "didn" })) return Result("no_match", 0);
        if (fact.Kind == "presence") return actual.Contains("yes") && actual.IsSubsetOf(wrapper.Append("yes"))
            ? Result("match", 1) : Result("insufficient", 0);
        var permitted = wrapper.Union(expected).ToHashSet();
        if (fact.Kind == "color")
        {
            var colors = actual.Intersect(Colors).ToHashSet();
            if (colors.Count > 0 && !colors.SetEquals(expected)) return Result("no_match", 0);
            if (expected.SetEquals(new[] { "black" })) permitted.UnionWith(new[] { "dark", "pitch" });
        }
        else if (fact.Kind == "location")
        {
            var positions = actual.Intersect(new[] { "top", "bottom", "front", "back", "left", "right", "inside", "outside" }).ToHashSet();
            if (positions.Count > 0 && !positions.IsSubsetOf(expected)) return Result("no_match", 0);
        }
        if (expected.IsSubsetOf(actual) && actual.IsSubsetOf(permitted)) return Result("match", 1);
        if (fact.Kind == "brand" && !actual.Overlaps(Colors) && !actual.Overlaps(expected)) return Result("no_match", 0);
        return actual.Overlaps(expected) ? Result("partial_match", .4) : Result("insufficient", 0);
    }
}
