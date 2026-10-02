using FoundU.Application.Support.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// The support assistant: answers what FoundU's help guide covers, and drafts a ticket for the
/// desk when it cannot help. Read-only - a ticket is only opened when the person sends it.
/// </summary>
public interface ISupportAssistantService
{
    Task<SupportAssistantResponse> AskAsync(SupportAssistantRequest request, Guid userId, CancellationToken cancellationToken = default);
}

public interface ISupportAgentClient
{
    /// <summary>Null means unavailable or invalid: the person can always send a ticket instead.</summary>
    Task<SupportAgentResult?> RunAsync(SupportAgentRequest request, CancellationToken cancellationToken = default);
}
