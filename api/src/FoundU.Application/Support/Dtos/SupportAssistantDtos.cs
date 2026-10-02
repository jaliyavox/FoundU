namespace FoundU.Application.Support.Dtos;

// ---------------------------------------------------------------- the page's side

public record SupportAssistantTurn(string Role, string Text);

/// <summary>
/// One message to the support assistant. There is no server session: the page sends the
/// conversation so far, and the guide entry the last answer came from.
/// </summary>
public record SupportAssistantRequest(
    string Message,
    IReadOnlyList<SupportAssistantTurn>? History = null,
    string? LastTopic = null);

/// <summary>A ticket the assistant prepared. The person reviews it and sends it - it is never sent for them.</summary>
public record SupportTicketDraft(string Subject, string Category, string Body);

/// <summary>
/// Phase: <c>answered</c> (a guide entry fits), <c>clarify</c> (asks for more), <c>escalate</c>
/// (it cannot fix this - <see cref="Ticket"/> is the draft), or <c>unavailable</c> (the AI
/// service is down; the draft is the person's own words, so they are never stuck).
/// </summary>
public record SupportAssistantResponse(
    string Phase,
    string Reply,
    string? Topic,
    SupportTicketDraft? Ticket);

// ---------------------------------------------------------------- the agent's side

public record SupportClaimSummary(string Item, string Status);
public record SupportReportSummary(string Item, string Status, bool Paused);
public record SupportHandoverSummary(string Item, string Status, string Role, int? HoursLeft);

/// <summary>
/// The person's own records as names and statuses. Never a collection or handover code, an
/// answer to a verification question, or anything belonging to another student.
/// </summary>
public record SupportAgentContext(
    IReadOnlyList<SupportClaimSummary> Claims,
    IReadOnlyList<SupportReportSummary> Reports,
    IReadOnlyList<SupportHandoverSummary> Handovers);

public record SupportAgentRequest(
    IReadOnlyList<SupportAssistantTurn> History,
    SupportAgentContext Context,
    string? LastTopic);

public record SupportAgentResult(string Phase, string Reply, string? Topic, SupportTicketDraft? Ticket);
