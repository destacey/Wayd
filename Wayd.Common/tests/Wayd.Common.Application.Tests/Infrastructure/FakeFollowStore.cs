using Wayd.Common.Application.Following;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// An <see cref="IFollowStore"/> over the fake context's follows, with the same already-exists semantics
/// as the real store. The concurrent-insert path is covered against SQL Server in
/// <c>FollowStoreIntegrationTests</c>.
/// </summary>
public sealed class FakeFollowStore(FakeWaydDbContext dbContext) : IFollowStore
{
    private readonly FakeWaydDbContext _dbContext = dbContext;

    public Task<bool> Add(Follow follow, CancellationToken cancellationToken)
    {
        if (_dbContext.Follows.Any(f => f.OwnerType == follow.OwnerType && f.OwnerId == follow.OwnerId && f.EmployeeId == follow.EmployeeId))
        {
            return Task.FromResult(false);
        }

        _dbContext.Follows.Add(follow);
        return Task.FromResult(true);
    }

    public Task<bool> Remove(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken)
    {
        var existing = _dbContext.Follows
            .FirstOrDefault(f => f.OwnerType == ownerType && f.OwnerId == ownerId && f.EmployeeId == employeeId);

        if (existing is null)
        {
            return Task.FromResult(false);
        }

        _dbContext.Follows.Remove(existing);
        return Task.FromResult(true);
    }
}
