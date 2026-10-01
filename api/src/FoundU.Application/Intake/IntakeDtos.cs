namespace FoundU.Application.Intake;

// Slots are untrusted search hints, never proof of ownership or authorization.
// Intent is which side of the counter the student is on: "lost" (an owner looking, the default)
// or "found" (a finder holding someone else's item).
public record IntakeSlots(string? ItemType = null, string? Colour = null, string? Location = null,
    string? When = null, string? Distinctive = null, string? Intent = null)
{
    public const string Lost = "lost";
    public const string Found = "found";

    public static bool IsValidIntent(string? intent) => intent is null or Lost or Found;
}
public record IntakeRequest(string Message, IntakeSlots? Slots = null);
public record IntakeDraft(Guid? CategoryId, Guid? ItemTypeId, Guid? LocationId,
    string Description, string? PrimaryColor, string? When);
// Kind is "desk" or "post" for a found item shown to an owner, and "lost" for an open lost
// report shown to a finder - the same fields the public feed already shows.
public record IntakeMatch(Guid Id, string Kind, string ItemType, string? Colour,
    string Location, string Description);
public record IntakeResponse(string Phase, string Reply, IntakeSlots Slots, IntakeDraft Draft,
    IntakeMatch? Match = null);

public record IntakeTurn(string Role, string Text);
public record IntakeVocabulary(IReadOnlyList<string> ItemTypes, IReadOnlyList<string> Locations);
public record IntakeAgentRequest(IReadOnlyList<IntakeTurn> History, IntakeSlots Slots,
    IntakeVocabulary Vocabulary, IReadOnlyList<IntakeMatch>? Candidates = null);
public record IntakeAgentResult(string Reply, IntakeSlots Slots, string Phase,
    string? MatchCandidateId = null, double? MatchConfidence = null);

public interface IIntakeService
{
    Task<IntakeResponse> AskAsync(IntakeRequest request, Guid studentId, CancellationToken cancellationToken = default);
}

public interface IIntakeAgentClient
{
    // Null means unavailable/invalid: the student can always continue with a manual report.
    Task<IntakeAgentResult?> RunAsync(IntakeAgentRequest request, CancellationToken cancellationToken = default);
}
