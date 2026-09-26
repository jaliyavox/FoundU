using FoundU.Application.Handovers.Dtos;

namespace FoundU.Application.Abstractions;

public interface IHandoverService
{
    /// <summary>
    /// "I will take it to security." Mints the code both sides quote, pauses the report and
    /// tells the owner. Pressing it twice returns the same handover rather than a second one.
    /// </summary>
    Task<HandoverDto> StartAsync(Guid reportId, Guid finderId, CancellationToken cancellationToken = default);

    /// <summary>The finder changing their mind before a desk has the item.</summary>
    Task<HandoverDto> CancelAsync(Guid reportId, Guid finderId, CancellationToken cancellationToken = default);

    /// <summary>What the finder or the owner sees, code included. Nobody else may ask.</summary>
    Task<HandoverDto?> GetForUserAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Staff typing a code in. Returns what the item is and what to do next - never the code.</summary>
    Task<HandoverLookupDto> LookupAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>The desk taking custody: logs the item, links it to the report, tells the owner where it is.</summary>
    Task<HandoverLookupDto> ReceiveAsync(string code, Guid staffId, ReceiveHandoverRequest request, CancellationToken cancellationToken = default);

    /// <summary>The desk releasing it to its owner, after checking who they are.</summary>
    Task<HandoverLookupDto> ReleaseAsync(string code, Guid staffId, ReleaseHandoverRequest request, CancellationToken cancellationToken = default);
}
