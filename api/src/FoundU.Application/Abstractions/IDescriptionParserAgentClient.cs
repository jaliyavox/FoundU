using FoundU.Application.LostReports.Dtos;

namespace FoundU.Application.Abstractions;

/// <summary>
/// Server-to-server boundary for optional lost-description enrichment. Implementations return a
/// bounded parse only and cannot create, flag, withdraw, or otherwise mutate a report.
/// </summary>
public interface IDescriptionParserAgentClient
{
    Task<DescriptionParserAgentCallResult<DescriptionParserAgentResult>> ParseAsync(
        string description,
        string correlationId,
        CancellationToken cancellationToken = default);
}
