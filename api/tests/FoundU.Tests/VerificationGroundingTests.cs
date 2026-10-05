using FoundU.Infrastructure.Claims;

namespace FoundU.Tests;

public sealed class VerificationGroundingTests
{
    public const string Bottle = "Nike stainless steel water bottle with a black screw cap. It has a small scratch near the bottom and a white sticker on one side.";

    [Fact]
    public void DecisionReasonsKeepPrivacyValidationWithoutQuestionGrammar()
    {
        Assert.True(SafeVerificationFallback.IsPrivateSafe("Identity verified at the desk.", [Bottle]));
        Assert.False(SafeVerificationFallback.IsSafe("Identity verified at the desk.", [Bottle]));
        Assert.False(SafeVerificationFallback.IsPrivateSafe("The bottle was Nike.", [Bottle]));
    }

    [Theory]
    [InlineData("What brand is the bottle?")]
    [InlineData("What color is the bottle cap?")]
    [InlineData("Is there any noticeable scratch or damage on the bottle?")]
    [InlineData("Where is the noticeable scratch located?")]
    [InlineData("Is there any sticker or label on the bottle?")]
    [InlineData("What color is the sticker?")]
    [InlineData("Where is the sticker located?")]
    public void ExplicitAtomicFactsAreSupported(string question)
        => Assert.True(SafeVerificationFallback.IsSafe(question, [Bottle]));

    [Theory]
    [InlineData("What identifying mark is underneath the bottle cap?")]
    [InlineData("What is written on the white sticker?")]
    [InlineData("What text is on the sticker?")]
    [InlineData("What shape is the sticker?")]
    [InlineData("What is inside the bottle?")]
    [InlineData("Where is the hidden compartment?")]
    [InlineData("What engraving does the bottle have?")]
    [InlineData("What is the serial number?")]
    [InlineData("What color is the cap and where is the sticker?")]
    [InlineData("Where is the scratch behind the sticker?")]
    public void UnsupportedConceptsAreRejected(string question)
    {
        Assert.False(SafeVerificationFallback.IsSafe(question, [Bottle]));
        Assert.Equal("What color is the bottle cap?", SafeVerificationFallback.Question(Bottle));
    }

    [Theory]
    [InlineData("A Samsung phone with a blue sticker on the back.", "What color is the sticker?")]
    [InlineData("A backpack has a scratch near the zipper and a red strap.", "Where is the noticeable scratch located?")]
    [InlineData("An Adidas shoe with a white sticker on the sole.", "Where is the sticker located?")]
    public void OtherItemsHaveGroundedFallbacks(string detail, string question)
    {
        Assert.True(SafeVerificationFallback.IsSafe(question, [detail]));
        Assert.True(SafeVerificationFallback.IsSafe(SafeVerificationFallback.Question(detail), [detail]));
        Assert.False(SafeVerificationFallback.IsSafe("What is written on the sticker?", [detail]));
    }

    [Theory]
    [InlineData("A white cap and a black sticker.", "What color is the sticker?", true)]
    [InlineData("A white cap. The sticker is on the back.", "What color is the sticker?", false)]
    [InlineData("A sticker and a scratch on the back.", "Where is the sticker located?", false)]
    [InlineData("A scratch. A cap near the bottom.", "Where is the noticeable scratch located?", false)]
    [InlineData("A cap with no sticker.", "What color is the sticker?", false)]
    [InlineData("A white sticker-less cap.", "What color is the sticker?", false)]
    [InlineData("A cap lacking a white sticker.", "What color is the sticker?", false)]
    [InlineData("Nike is written on a sticker.", "What brand is the item?", false)]
    [InlineData("A bottle. A black cap on a pen.", "What color is the bottle cap?", false)]
    [InlineData("A bag with an apple inside.", "What brand is the item?", false)]
    public void FactsCannotBeRelocated(string detail, string question, bool supported)
        => Assert.Equal(supported, SafeVerificationFallback.IsSafe(question, [detail]));

    // The API image is built from api/ alone, so it embeds its own copy of the Python grammar.
    [Fact]
    public void EmbeddedGrammarMatchesThePythonCopy()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "ai", "app", "agents", "verification_grounding.json")))
            root = root.Parent ?? throw new InvalidOperationException("Repository root not found.");
        Assert.Equal(
            File.ReadAllText(Path.Combine(root.FullName, "ai", "app", "agents", "verification_grounding.json")),
            File.ReadAllText(Path.Combine(root.FullName, "api", "src", "FoundU.Infrastructure", "Claims", "verification_grounding.json")));
    }
}
