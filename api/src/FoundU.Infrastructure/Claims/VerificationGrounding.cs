using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FoundU.Infrastructure.Claims;

/// <summary>Fail-closed question grammar shared with Python. Values never enter wording.</summary>
internal static class VerificationGrounding
{
    public const string GenericQuestion = "What identifying detail can you provide about the item?";
    private sealed record Rule(string Pattern, List<string> Questions,
        [property: JsonPropertyName("answer_kind")] string AnswerKind,
        [property: JsonPropertyName("answer_pattern")] string AnswerPattern,
        [property: JsonPropertyName("answer_group")] int AnswerGroup,
        [property: JsonPropertyName("presence_questions")] List<string> PresenceQuestions);
    internal sealed record AnswerFact(string Kind, string Value, string Subject = "item");
    private static readonly List<Rule> Rules = LoadRules();

    private static List<Rule> LoadRules()
    {
        using var stream = typeof(VerificationGrounding).Assembly.GetManifestResourceStream("VerificationGrounding.json")!;
        return JsonSerializer.Deserialize<List<Rule>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static IReadOnlyList<string> Candidates(string detail)
    {
        var clauses = Regex.Split(detail.ToLowerInvariant(), @"[.!?;\n]|\band\b|\bbut\b")
            .Where(c => !Regex.IsMatch(c, @"\b(no|not|without|lacks|lacking|missing|none|maybe|possibly|might|could|unknown)\b|\b\w+-less\b|\b(?:isn|wasn|doesn|didn)'t\b")).ToList();
        return Rules.Where(rule => clauses.Any(c => Regex.IsMatch(c, rule.Pattern)))
            .SelectMany(rule => rule.Questions)
            .Where(q => !q.Contains("bottle", StringComparison.Ordinal) || Regex.IsMatch(detail.ToLowerInvariant(), @"\bbottle\b"))
            .Distinct(StringComparer.Ordinal).ToList();
    }

    public static bool IsGrounded(string question, string detail)
        => !string.IsNullOrWhiteSpace(detail) && Candidates(detail).Append(GenericQuestion)
            .Any(q => Normalize(q) == Normalize(question));

    private static string Normalize(string value)
        => Regex.Replace(value.ToLowerInvariant().Replace("colour", "color"), @"[^a-z0-9]+", " ").Trim();

    public static AnswerFact? ResolveAnswerFact(string question, string detail)
    {
        if (!IsGrounded(question, detail)) return null;
        var subject = new[] { "cap", "sticker", "scratch", "damage", "bottle" }
            .FirstOrDefault(s => Normalize(question).Split(' ').Contains(s)) ?? "item";
        var facts = new HashSet<AnswerFact>();
        var clauses = Regex.Split(detail.ToLowerInvariant(), @"[.!?;\n]|\band\b|\bbut\b")
            .Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        foreach (var rule in Rules.Where(r => r.Questions.Any(q => Normalize(q) == Normalize(question))))
        {
            foreach (var clause in clauses.Where(c => Candidates(c).Any(q => Normalize(q) == Normalize(question))))
            {
                foreach (Match match in Regex.Matches(clause, rule.AnswerPattern))
                {
                    var presence = rule.PresenceQuestions.Any(q => Normalize(q) == Normalize(question));
                    var kind = presence ? "presence" : rule.AnswerKind;
                    var value = presence ? "yes" : rule.AnswerGroup == -1
                        ? match.Groups.Cast<Group>().Skip(1).FirstOrDefault(g => g.Success)?.Value
                        : rule.AnswerGroup > 0 ? match.Groups[rule.AnswerGroup].Value : clause;
                    if (!string.IsNullOrWhiteSpace(value)) facts.Add(new(kind, value.Trim(), subject));
                }
            }
        }
        if (facts.Count == 0 && Normalize(question) == Normalize(GenericQuestion) && clauses.Count == 1)
            return new("description", clauses[0]);
        return facts.Count == 1 ? facts.Single() : null;
    }
}
