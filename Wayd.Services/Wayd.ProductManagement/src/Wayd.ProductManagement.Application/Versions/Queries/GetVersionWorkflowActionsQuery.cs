using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.DeliveryWorkflows;
using Wayd.ProductManagement.Domain;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// The lifecycle actions a version's workflow allows from the status it holds.
/// </summary>
/// <remarks>
/// A restricted workflow can leave out a move a company forbids — withdrawing a released version, say —
/// so a page cannot offer an action from the version's state alone. This answers for the workflow; the
/// version's own rules still decide whether the action succeeds.
/// </remarks>
public sealed record GetVersionWorkflowActionsQuery : IQuery<IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    public GetVersionWorkflowActionsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Version>();
    }

    public Expression<Func<Version, bool>> IdOrKeyFilter { get; }
}

public sealed class GetVersionWorkflowActionsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetVersionWorkflowActionsQuery, IReadOnlyCollection<DeliveryWorkflowActionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    /// <returns><c>null</c> when no such version exists, so the caller can answer 404.</returns>
    public async Task<IReadOnlyCollection<DeliveryWorkflowActionDto>?> Handle(
        GetVersionWorkflowActionsQuery request, CancellationToken cancellationToken)
    {
        var version = await _productManagementDbContext.Versions
            .Where(request.IdOrKeyFilter)
            .Select(v => new { v.StatusId, v.CutAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return null;
        }

        var workflow = await _statusResolver.ForScope(ProductWorkflowOwners.Version.Key, scopeId: null, cancellationToken);

        // Nothing rather than a failure: the action itself reports a misconfigured workflow, with the reason.
        if (workflow.IsFailure)
        {
            return [];
        }

        var w = workflow.Value;

        // Each target as its command resolves it. Reverting returns a version that was never cut to the
        // start of its workflow, as RevertVersionReleaseCommand does.
        return DeliveryWorkflowActions.Allowed(
            w,
            version.StatusId,
            (DeliveryAction.Cut, w.StatusFor((int)ProductStatusAlias.Ready)),
            (DeliveryAction.MarkReleased, w.StatusFor((int)ProductStatusAlias.Released)),
            (DeliveryAction.RevertRelease, version.CutAt is null ? w.InitialStatus : w.StatusFor((int)ProductStatusAlias.Ready)),
            (DeliveryAction.Withdraw, w.StatusFor((int)ProductStatusAlias.Withdrawn)));
    }
}
