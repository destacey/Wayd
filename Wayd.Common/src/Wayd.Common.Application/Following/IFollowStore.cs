using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following;

/// <summary>
/// Writes follows, treating "already following" and "not following" as outcomes rather than errors.
/// </summary>
/// <remarks>
/// A seam because idempotence under concurrency needs the database's word: two follows of one record
/// by one person racing each other both pass an existence check, and only the unique key can say which
/// one lost. Recognising that violation is provider-specific, so it lives in Infrastructure.
/// </remarks>
public interface IFollowStore
{
    /// <summary>
    /// Saves the follow unless the same person already follows the same record.
    /// </summary>
    /// <returns><c>true</c> when the follow was added; <c>false</c> when it already existed.</returns>
    Task<bool> Add(Follow follow, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the person's follow of the record, if there is one.
    /// </summary>
    /// <returns><c>true</c> when a follow was removed; <c>false</c> when there was none.</returns>
    Task<bool> Remove(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken);
}
