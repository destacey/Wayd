using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Following.Queries;

/// <summary>
/// One record the caller follows, in full, with what has happened to it since they followed it. Null when the
/// caller does not follow it or can no longer read it; fails for an owner type that cannot be followed.
/// </summary>
/// <remarks>
/// The record is reached through the same readable-follows query as the list, so it resolves only while the
/// caller both follows it and can read it. Its activity is read only once that is established, through its
/// read rule, so it holds exactly what the record's own Activity section shows the caller.
/// </remarks>
public sealed record GetFollowedRecordQuery(string OwnerType, Guid OwnerId) : IQuery<Result<FollowedRecordDetailDto?>>;

/// <summary>Rejects a missing owner type or id before the handler resolves either.</summary>
public sealed class GetFollowedRecordQueryValidator : CustomValidator<GetFollowedRecordQuery>
{
    public GetFollowedRecordQueryValidator()
    {
        RuleFor(q => q.OwnerType).NotEmpty().MaximumLength(FollowOwnerDescriptor.KeyMaxLength);
        RuleFor(q => q.OwnerId).NotEmpty();
    }
}

/// <summary>Reads one followed record and its activity since following.</summary>
public sealed class GetFollowedRecordQueryHandler(
    INotificationsDbContext notificationsDbContext,
    IActivityLogDbContext activityLogDbContext,
    IActivityLogReader activityLogReader,
    IEnumerable<IRecordReadRule> readRules,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetFollowedRecordQuery, Result<FollowedRecordDetailDto?>>
{
    /// <summary>The most activity entries returned with a record.</summary>
    public const int ChangeLimit = 20;

    private readonly INotificationsDbContext _notificationsDbContext = notificationsDbContext;
    private readonly IActivityLogDbContext _activityLogDbContext = activityLogDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;
    private readonly IEnumerable<IRecordReadRule> _readRules = readRules;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result<FollowedRecordDetailDto?>> Handle(GetFollowedRecordQuery request, CancellationToken cancellationToken)
    {
        var resolved = FollowReadRules.Resolve(_readRules, request.OwnerType);
        if (resolved.IsFailure)
        {
            return Result.Failure<FollowedRecordDetailDto?>(resolved.Error);
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
        {
            return Result.Success<FollowedRecordDetailDto?>(null);
        }

        var rows = await FollowedRows.Build(
            _notificationsDbContext.Follows,
            _activityLogDbContext.ActivityLogs,
            employeeId.Value,
            [resolved.Value],
            null,
            cancellationToken);
        var row = rows is null
            ? null
            : await rows.FirstOrDefaultAsync(r => r.OwnerId == request.OwnerId, cancellationToken);
        if (row is null)
        {
            return Result.Success<FollowedRecordDetailDto?>(null);
        }

        var described = await resolved.Value.ReadRule.Describe([row.OwnerId], cancellationToken);
        if (!described.TryGetValue(row.OwnerId, out var summary))
        {
            return Result.Success<FollowedRecordDetailDto?>(null);
        }

        // Newest first, so the entries since following are a prefix of the first page.
        var activity = await resolved.Value.ReadRule.ReadActivity(
            row.OwnerId,
            _activityLogReader,
            new PageRequest(PageSize: ChangeLimit),
            cancellationToken);
        var changes = activity.Items.Where(a => a.Timestamp >= row.FollowedOn).ToList();

        var isMuted = await _notificationsDbContext.RecordMutes.AnyAsync(
            m => m.OwnerType == row.OwnerType && m.OwnerId == row.OwnerId && m.EmployeeId == employeeId.Value,
            cancellationToken);

        return new FollowedRecordDetailDto
        {
            Record = FollowedRecordDto.Create(row.OwnerType, row.OwnerId, row.FollowedOn, row.LastChangedOn, isMuted, summary),
            Progress = summary.Progress,
            People = [.. summary.People],
            Start = summary.Start,
            End = summary.End,
            ChangesSinceFollowed = changes,
            HasMoreChanges = changes.Count == activity.Items.Count && activity.HasNextPage,
        };
    }
}
