namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Returns where a workspace's work item history sync left off, and whether a sync has ever read the history
/// through to the end.
/// </summary>
/// <param name="WorkspaceId">The workspace.</param>
public sealed record GetWorkItemHistoryWatermarkQuery(Guid WorkspaceId) : IQuery<Result<WorkItemHistoryCursor>>;

/// <param name="Watermark">
/// The source's token for where the sync left off; null when the history has never been synced, or a full
/// sync cleared it to replay.
/// </param>
/// <param name="ReadToEnd">
/// Whether a sync has read the history through to the source's last batch. Until one has, an empty last
/// batch is still applied, since applying it is what records this.
/// </param>
public sealed record WorkItemHistoryCursor(string? Watermark, bool ReadToEnd);

public sealed class GetWorkItemHistoryWatermarkQueryValidator : CustomValidator<GetWorkItemHistoryWatermarkQuery>
{
    public GetWorkItemHistoryWatermarkQueryValidator()
    {
        RuleFor(q => q.WorkspaceId)
            .NotEmpty();
    }
}
