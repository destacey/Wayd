using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// The lifecycle actions a release's workflow allows from the status it holds.
/// </summary>
/// <remarks>
/// A restricted workflow can leave out a move a company forbids — reverting a release, say — so a page
/// cannot offer an action from the release's state alone. This answers for the workflow; the release's
/// own rules still decide whether the action succeeds.
/// </remarks>
public sealed record GetReleaseWorkflowActionsQuery : IQuery<IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    public GetReleaseWorkflowActionsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Release>();
    }

    public Expression<Func<Release, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleaseWorkflowActionsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetReleaseWorkflowActionsQuery, IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    /// <returns><c>null</c> when no such release exists, so the caller can answer 404.</returns>
    public async Task<IReadOnlyCollection<DeliveryWorkflowActionDto>?> Handle(
        GetReleaseWorkflowActionsQuery request, CancellationToken cancellationToken)
    {
        var statusId = await _productManagementDbContext.Releases
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.StatusId)
            .FirstOrDefaultAsync(cancellationToken);

        if (statusId is null)
        {
            return null;
        }

        var workflow = await _statusResolver.ForScope(ProductWorkflowOwners.Release.Key, scopeId: null, cancellationToken);

        // Nothing rather than a failure: the action itself reports a misconfigured workflow, with the reason.
        if (workflow.IsFailure)
        {
            return [];
        }

        var w = workflow.Value;

        return DeliveryWorkflowActions.Allowed(
            w,
            statusId.Value,
            (DeliveryAction.MarkReleased, w.StatusFor((int)ProductStatusAlias.Released)),
            (DeliveryAction.RevertRelease, w.StatusFor((int)ProductStatusAlias.Ready)),
            (DeliveryAction.Withdraw, w.StatusFor((int)ProductStatusAlias.Withdrawn)));
    }
}
