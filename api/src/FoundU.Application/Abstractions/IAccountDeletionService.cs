namespace FoundU.Application.Abstractions;

/// <summary>
/// An administrator deleting an account. The person's open work is closed the same way they
/// would close it themselves, their personal details are erased, and the account is removed
/// from every list and query. Records other people rely on - claims already decided, items at
/// the desk, the audit trail - keep their history, attributed to "Deleted user".
/// </summary>
public interface IAccountDeletionService
{
    /// <summary>
    /// Refused for the acting admin's own account, for another admin (change their role first),
    /// for anyone who has decided claims (suspend them instead - decisions stay on record), and
    /// while an item is waiting at the desk for this person (it would be stranded).
    /// </summary>
    Task DeleteAsync(Guid userId, Guid actingAdminId, CancellationToken cancellationToken = default);
}
