namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Returns the source's token for where a workspace's work item history sync left off; null when
/// the history has never been synced, or a full sync cleared it to replay.
/// </summary>
/// <param name="WorkspaceId">The workspace.</param>
public sealed record GetWorkItemHistoryWatermarkQuery(Guid WorkspaceId) : IQuery<Result<string?>>;

public sealed class GetWorkItemHistoryWatermarkQueryValidator : CustomValidator<GetWorkItemHistoryWatermarkQuery>
{
    public GetWorkItemHistoryWatermarkQueryValidator()
    {
        RuleFor(q => q.WorkspaceId)
            .NotEmpty();
    }
}
