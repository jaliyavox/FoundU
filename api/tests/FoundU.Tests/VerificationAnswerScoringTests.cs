using FoundU.Application.Claims.Dtos;
using FoundU.Infrastructure.Claims;

namespace FoundU.Tests;

public sealed class VerificationAnswerScoringTests
{
    internal const string Evidence = "Nike stainless-steel water bottle with a black screw cap. It has a small scratch near the bottom and a white sticker on one side.";
    internal const string Cap = "What color is the bottle cap?";

    [Fact]
    public void MalformedIndividualResultsAreNotUsableEvenWithPlausibleAggregate()
    {
        VerificationAgentQuestion[] questions = [new("verification-1", "What sticker or label does the item have?")];
        var result = new EvaluateVerificationAnswersResult(Guid.NewGuid(), "likely_match", "unsafe", 100,
            Evaluations: [new("different-question", "match", 1)]);
        Assert.False(VerificationAnswerScoring.IsUsableResult(result, questions));
        Assert.False(VerificationAnswerScoring.IsUsableResult(result with { Evaluations = [new("verification-1", "no_match", 1)] }, questions));
        Assert.False(VerificationAnswerScoring.IsUsableResult(result with { Score = double.NaN }, questions));
        Assert.False(VerificationAnswerScoring.IsUsableResult(result with { Evaluations = null }, questions));
        Assert.False(VerificationAnswerScoring.IsUsableResult(result with
        {
            Evaluations = [new("verification-1", "match", 1)],
            ConflictingInformation = ["The evidence contradicts this answer."]
        }, questions));
    }

    internal static EvaluateVerificationAnswersResult Evaluate(params (string Question, string Answer)[] pairs)
        => SafeVerificationFallback.Evaluate(Guid.NewGuid(), new Dictionary<string, string> { ["staff_observation"] = Evidence },
            pairs.Select((p, i) => new VerificationAgentQuestion($"verification-{i + 1}", p.Question)).ToList(),
            pairs.Select((p, i) => new VerificationAgentAnswer($"verification-{i + 1}", p.Answer)).ToList());

    [Theory]
    [InlineData("Black")]
    [InlineData("The cap is black.")]
    [InlineData("It has a black cap.")]
    [InlineData("Black colour")]
    [InlineData("The bottle cap was black.")]
    [InlineData("Dark black")]
    public void ExactAndEquivalentAnswersStronglyMatch(string answer)
    {
        var result = Evaluate((Cap, answer));
        Assert.Equal(100, result.Score);
        Assert.Equal("match", result.Evaluations!.Single().Result);
        Assert.Empty(result.ConflictingInformation!);
        Assert.Single(result.MatchedEvidence!);
        Assert.DoesNotContain(Evidence, System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("White", "no_match")]
    [InlineData("Blue", "no_match")]
    [InlineData("Nike", "insufficient")]
    [InlineData("I don't know", "insufficient")]
    [InlineData("I don’t know", "insufficient")]
    [InlineData("", "insufficient")]
    [InlineData("   ", "insufficient")]
    [InlineData("Black or white", "no_match")]
    [InlineData("Not black", "no_match")]
    [InlineData("Maybe black", "insufficient")]
    [InlineData("Nike black", "partial_match")]
    [InlineData("The sticker is black", "partial_match")]
    public void WrongAndInsufficientAnswersDoNotPass(string answer, string expected)
    {
        var result = Evaluate((Cap, answer));
        Assert.Equal(expected, result.Evaluations!.Single().Result);
        Assert.True(result.Score < 75);
        Assert.Equal(expected == "no_match", result.ConflictingInformation!.Count > 0);
    }

    [Theory]
    [InlineData("What brand is the bottle?", "Nike", 100)]
    [InlineData("Where is the noticeable scratch located?", "Near the bottom", 100)]
    [InlineData("What color is the sticker?", "White", 100)]
    [InlineData("Where is the sticker located?", "On one side", 100)]
    [InlineData("What brand is the bottle?", "Black", 0)]
    [InlineData("Where is the noticeable scratch located?", "White", 0)]
    public void AnswersAreQuestionSpecific(string question, string answer, double score)
        => Assert.Equal(score, Evaluate((question, answer)).Score);

    [Fact]
    public void DescriptiveQuestionDoesNotIncludeUnrelatedFactsInSameClause()
    {
        var result = SafeVerificationFallback.Evaluate(Guid.NewGuid(),
            new Dictionary<string, string> { ["detail"] = "Nike bottle with a white sticker on one side." },
            [new("verification-1", "What sticker or label does the item have?")], [new("verification-1", "Nike")]);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void WrongLocationIsContradictory()
        => Assert.Equal("no_match", Evaluate(("Where is the noticeable scratch located?", "Near the top")).Evaluations!.Single().Result);

    [Theory]
    [InlineData("White", 100, "match")]
    [InlineData("Blue", 50, "no_match")]
    [InlineData("I don't know", 50, "insufficient")]
    public void EveryQuestionContributesToMean(string second, double score, string result)
    {
        var assessment = Evaluate((Cap, "Black"), ("What color is the sticker?", second));
        Assert.Equal(score, assessment.Score);
        Assert.Equal("match", assessment.Evaluations![0].Result);
        Assert.Equal(result, assessment.Evaluations[1].Result);
        Assert.Equal(result == "no_match", assessment.ConflictingInformation!.Count > 0);
    }

    [Theory]
    [InlineData("An Adidas shoe with a red sticker on the sole.", "What color is the sticker?", "Red")]
    [InlineData("A Samsung phone with a gray sticker on the back.", "What color is the sticker?", "Grey")]
    public void FallbackWorksForOtherObservations(string evidence, string question, string answer)
    {
        var result = SafeVerificationFallback.Evaluate(Guid.NewGuid(), new Dictionary<string, string> { ["new_observation"] = evidence },
            [new("verification-1", question)], [new("verification-1", answer)]);
        Assert.Equal(100, result.Score);
    }
}
