using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// The lifecycle actions a release package's workflow allows from the status it holds.
/// </summary>
/// <remarks>
/// A restricted workflow can leave out a move a company forbids — withdrawing a released package, say —
/// so a page cannot offer an action from the package's state alone. This answers for the workflow; the
/// package's own rules still decide whether the action succeeds.
/// </remarks>
public sealed record GetReleasePackageWorkflowActionsQuery : IQuery<IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    public GetReleasePackageWorkflowActionsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ReleasePackage>();
    }

    public Expression<Func<ReleasePackage, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleasePackageWorkflowActionsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetReleasePackageWorkflowActionsQuery, IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    /// <returns><c>null</c> when no such package exists, so the caller can answer 404.</returns>
    public async Task<IReadOnlyCollection<DeliveryWorkflowActionDto>?> Handle(
        GetReleasePackageWorkflowActionsQuery request, CancellationToken cancellationToken)
    {
        var statusId = await _productManagementDbContext.ReleasePackages
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.StatusId)
            .FirstOrDefaultAsync(cancellationToken);

        if (statusId is null)
        {
            return null;
        }

        var workflow = await _statusResolver.ForScope(ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, cancellationToken);

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
            (DeliveryAction.Withdraw, w.StatusFor((int)ProductStatusAlias.Withdrawn)));
    }
}
