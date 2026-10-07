namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// Returns the external ids of a workspace's work items whose stored revisions skip a revision
/// number, lowest id first. A source numbers each item's revisions without gaps, so a missing number
/// is a revision Wayd never read: one made while the item was in a project or of a work type the
/// workspace's own revisions do not cover. Items already filled at their current highest revision
/// are left out until a newer one arrives.
/// </summary>
/// <param name="WorkspaceId">The workspace whose work items to check.</param>
/// <param name="Limit">The most ids to return, which bounds the fetches one sync makes to fill them.</param>
public sealed record GetWorkItemsMissingRevisionsQuery(Guid WorkspaceId, int Limit) : IQuery<Result<IReadOnlyList<int>>>;

public sealed class GetWorkItemsMissingRevisionsQueryValidator : CustomValidator<GetWorkItemsMissingRevisionsQuery>
{
    public GetWorkItemsMissingRevisionsQueryValidator()
    {
        RuleFor(q => q.WorkspaceId)
            .NotEmpty();

        RuleFor(q => q.Limit)
            .GreaterThan(0);
    }
}
