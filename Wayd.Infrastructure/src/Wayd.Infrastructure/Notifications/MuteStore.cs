using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.Persistence.Extensions;

namespace Wayd.Infrastructure.Notifications;

/// <summary>
/// Writes mutes against SQL Server, where each table's primary key is what makes a concurrent duplicate mute
/// lose rather than add a second row.
/// </summary>
internal sealed class MuteStore(WaydDbContext dbContext) : IMuteStore
{
    private readonly WaydDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<bool> Add(RecordMute mute, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mute);

        var exists = await _dbContext.RecordMutes.AnyAsync(
            m => m.OwnerType == mute.OwnerType && m.OwnerId == mute.OwnerId && m.EmployeeId == mute.EmployeeId,
            cancellationToken);

        return !exists && await Save(mute, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveRecordMute(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken)
    {
        // Set-based: a mute is unevented, so there is nothing to load and nothing for a save to raise.
        var removed = await _dbContext.RecordMutes
            .Where(m => m.OwnerType == ownerType && m.OwnerId == ownerId && m.EmployeeId == employeeId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> Add(ReasonMute mute, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mute);

        var exists = await _dbContext.ReasonMutes.AnyAsync(
            m => m.EmployeeId == mute.EmployeeId && m.Reason == mute.Reason,
            cancellationToken);

        return !exists && await Save(mute, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveReasonMute(Guid employeeId, NotificationReason reason, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.ReasonMutes
            .Where(m => m.EmployeeId == employeeId && m.Reason == reason)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    /// <summary>Adds the mute, or reports that a concurrent mute of the same thing committed first.</summary>
    private async Task<bool> Save(object mute, CancellationToken cancellationToken)
    {
        _dbContext.Add(mute);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The row exists, which is the goal, so this is the already-muted outcome rather than an error.
            _dbContext.Entry(mute).State = EntityState.Detached;
            return false;
        }
    }
}
