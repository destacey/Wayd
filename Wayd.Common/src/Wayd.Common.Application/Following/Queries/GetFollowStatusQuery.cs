using Wayd.Common.Application.Records;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Queries;

/// <summary>
/// Whether the caller follows a record. Null when the record does not exist or the caller cannot read it;
/// fails for an owner type that cannot be followed.
/// </summary>
/// <remarks>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee follows nothing, which is a
/// truthful answer rather than a refusal.
/// </remarks>
public sealed record GetFollowStatusQuery(string OwnerType, Guid OwnerId) : IQuery<Result<FollowStatusDto?>>;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class GetFollowStatusQueryValidator : CustomValidator<GetFollowStatusQuery>
{
    public GetFollowStatusQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
    }
}

/// <summary>Answers whether the caller follows a record they can read.</summary>
public sealed class GetFollowStatusQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetFollowStatusQuery, Result<FollowStatusDto?>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<FollowStatusDto?>> Handle(GetFollowStatusQuery request, CancellationToken cancellationToken)
    {
        var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
        if (resolved.IsFailure)
        {
            return Result.Failure<FollowStatusDto?>(resolved.Error);
        }

        var (descriptor, readRule) = resolved.Value;

        if (!await FollowReadRules.CanRead(readRule, request.OwnerId, cancellationToken))
        {
            return Result.Success<FollowStatusDto?>(null);
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return new FollowStatusDto { IsFollowing = false };
        }

        var ownerType = descriptor.Key;
        var followedOn = await _notificationsDbContext.Follows
            .Where(f => f.OwnerType == ownerType && f.OwnerId == request.OwnerId && f.EmployeeId == employeeId.Value)
            .Select(f => (Instant?)f.FollowedOn)
            .FirstOrDefaultAsync(cancellationToken);

        return new FollowStatusDto { IsFollowing = followedOn is not null, FollowedOn = followedOn };
    }
}
