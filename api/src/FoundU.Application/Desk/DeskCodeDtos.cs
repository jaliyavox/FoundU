namespace FoundU.Application.Desk;

/// <summary>
/// What a six-digit code a finder quotes at the desk turned out to be.
///
/// <list type="bullet">
/// <item><c>found-post</c> - the code on their own found post; <see cref="Id"/> is the post.</item>
/// <item><c>handover</c> - the code from pressing "I found this" and choosing security on a lost
/// report; the desk receives it on the handover page.</item>
/// <item><c>lost-report</c> - the code printed on someone's lost report; the desk logs the item
/// with it, which links the two and tells the owner. <see cref="Id"/> is the report.</item>
/// </list>
///
/// The three code series are generated independently, so one code can - rarely - mean two
/// things. Every match is returned and the desk picks.
/// </summary>
public record DeskCodeMatch(
    string Kind,
    Guid Id,
    string Title,
    string Detail,
    Guid? CategoryId = null,
    Guid? ItemTypeId = null,
    string? PrimaryColor = null);

public interface IDeskCodeService
{
    Task<IReadOnlyList<DeskCodeMatch>> ResolveAsync(string code, CancellationToken cancellationToken = default);
}
