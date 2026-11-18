using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.PlanningIntervals;

/// <summary>
/// Who may read and change a planning interval objective's links: the Planning Interval Objectives View permission
/// reads them, and the Manage permission, which edits the objective, changes them.
/// </summary>
public sealed class PlanningIntervalObjectiveLinkOwnerType(IPlanningDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : PermissionLinkOwnerType(currentPrincipal)
{
    private readonly IPlanningDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public override string OwnerType => PlanningLinkOwners.PlanningIntervalObjective.Key;

    /// <inheritdoc/>
    protected override string ViewPermission { get; } = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervalObjectives);

    /// <inheritdoc/>
    protected override string ManagePermission { get; } = ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.PlanningIntervalObjectives);

    /// <inheritdoc/>
    protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.PlanningIntervalObjectives.AnyAsync(o => o.Id == ownerId, cancellationToken);
}
