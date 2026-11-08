namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Returns whether a managed workspace's next sync must be a full one, whatever kind was asked for.
/// </summary>
/// <param name="WorkspaceId">The workspace.</param>
public sealed record GetWorkspaceNeedsFullSyncQuery(Guid WorkspaceId) : IQuery<Result<bool>>;

/// <summary>Requires the workspace.</summary>
public sealed class GetWorkspaceNeedsFullSyncQueryValidator : CustomValidator<GetWorkspaceNeedsFullSyncQuery>
{
    public GetWorkspaceNeedsFullSyncQueryValidator()
    {
        RuleFor(q => q.WorkspaceId)
            .NotEmpty();
    }
}
