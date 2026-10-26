using Wayd.Common.Application.Following;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Notifications.Queries;

/// <summary>
/// Whether the caller has muted a record. Null when the record does not exist or the caller cannot read it;
/// fails for an owner type that cannot be followed.
/// </summary>
/// <remarks>
/// Not <see cref="IRequireLinkedEmployee"/>: a caller with no employee has muted nothing, which is a truthful
/// answer rather than a refusal.
/// </remarks>
public sealed record GetRecordMuteStatusQuery(string OwnerType, Guid OwnerId) : IQuery<Result<RecordMuteStatusDto?>>;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class GetRecordMuteStatusQueryValidator : CustomValidator<GetRecordMuteStatusQuery>
{
    public GetRecordMuteStatusQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
    }
}

/// <summary>Answers whether the caller has muted a record they can read.</summary>
public sealed class GetRecordMuteStatusQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetRecordMuteStatusQuery, Result<RecordMuteStatusDto?>>
{
    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<RecordMuteStatusDto?>> Handle(GetRecordMuteStatusQuery request, CancellationToken cancellationToken)
    {
        var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
        if (resolved.IsFailure)
        {
            return Result.Failure<RecordMuteStatusDto?>(resolved.Error);
        }

        var (descriptor, readRule) = resolved.Value;

        if (!await FollowReadRules.CanRead(readRule, request.OwnerId, cancellationToken))
        {
            return Result.Success<RecordMuteStatusDto?>(null);
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return new RecordMuteStatusDto { IsMuted = false };
        }

        var ownerType = descriptor.Key;
        var mutedOn = await _notificationsDbContext.RecordMutes
            .Where(m => m.OwnerType == ownerType && m.OwnerId == request.OwnerId && m.EmployeeId == employeeId.Value)
            .Select(m => (Instant?)m.MutedOn)
            .FirstOrDefaultAsync(cancellationToken);

        return new RecordMuteStatusDto { IsMuted = mutedOn is not null, MutedOn = mutedOn };
    }
}
