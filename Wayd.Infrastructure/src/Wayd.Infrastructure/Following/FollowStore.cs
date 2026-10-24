using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Following;
using Wayd.Common.Domain.Following;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Following;

/// <summary>
/// Writes follows against SQL Server, where the table's primary key is what makes a concurrent duplicate
/// follow lose rather than add a second row.
/// </summary>
internal sealed class FollowStore(WaydDbContext dbContext) : IFollowStore
{
    private readonly WaydDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<bool> Add(Follow follow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(follow);

        if (await Exists(follow.OwnerType, follow.OwnerId, follow.EmployeeId, cancellationToken))
        {
            return false;
        }

        _dbContext.Follows.Add(follow);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // A concurrent follow of the same record by the same person committed first. The goal — the
            // row exists — is met, so this is the already-following outcome, not an error.
            _dbContext.Entry(follow).State = EntityState.Detached;
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> Remove(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken)
    {
        // Set-based: Follow is unevented, so there is no aggregate to load and nothing for a save to raise.
        var removed = await _dbContext.Follows
            .Where(f => f.OwnerType == ownerType && f.OwnerId == ownerId && f.EmployeeId == employeeId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    /// <summary>Whether the person already follows the record.</summary>
    private Task<bool> Exists(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken) =>
        _dbContext.Follows.AnyAsync(
            f => f.OwnerType == ownerType && f.OwnerId == ownerId && f.EmployeeId == employeeId,
            cancellationToken);
}
