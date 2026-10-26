using Wayd.Common.Application.Notifications;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// An <see cref="IMuteStore"/> over the fake context's mutes, with the same already-exists semantics as the
/// real store. The concurrent-insert path is covered against SQL Server in <c>MuteStoreIntegrationTests</c>.
/// </summary>
public sealed class FakeMuteStore(FakeWaydDbContext dbContext) : IMuteStore
{
    private readonly FakeWaydDbContext _dbContext = dbContext;

    public Task<bool> Add(RecordMute mute, CancellationToken cancellationToken)
    {
        if (_dbContext.RecordMutes.Any(m => m.OwnerType == mute.OwnerType && m.OwnerId == mute.OwnerId && m.EmployeeId == mute.EmployeeId))
        {
            return Task.FromResult(false);
        }

        _dbContext.RecordMutes.Add(mute);
        return Task.FromResult(true);
    }

    public Task<bool> RemoveRecordMute(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken)
    {
        var existing = _dbContext.RecordMutes
            .FirstOrDefault(m => m.OwnerType == ownerType && m.OwnerId == ownerId && m.EmployeeId == employeeId);

        if (existing is null)
        {
            return Task.FromResult(false);
        }

        _dbContext.RecordMutes.Remove(existing);
        return Task.FromResult(true);
    }

    public Task<bool> Add(ReasonMute mute, CancellationToken cancellationToken)
    {
        if (_dbContext.ReasonMutes.Any(m => m.EmployeeId == mute.EmployeeId && m.Reason == mute.Reason))
        {
            return Task.FromResult(false);
        }

        _dbContext.ReasonMutes.Add(mute);
        return Task.FromResult(true);
    }

    public Task<bool> RemoveReasonMute(Guid employeeId, NotificationReason reason, CancellationToken cancellationToken)
    {
        var existing = _dbContext.ReasonMutes.FirstOrDefault(m => m.EmployeeId == employeeId && m.Reason == reason);

        if (existing is null)
        {
            return Task.FromResult(false);
        }

        _dbContext.ReasonMutes.Remove(existing);
        return Task.FromResult(true);
    }
}
