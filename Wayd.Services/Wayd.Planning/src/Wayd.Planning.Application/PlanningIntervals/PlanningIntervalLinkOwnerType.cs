using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.PlanningIntervals;

/// <summary>
/// Who may read and change a planning interval's links: the Planning Intervals View permission reads them, and the
/// Update permission, which edits the interval, changes them.
/// </summary>
public sealed class PlanningIntervalLinkOwnerType(IPlanningDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : PermissionLinkOwnerType(currentPrincipal)
{
    private readonly IPlanningDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public override string OwnerType => PlanningLinkOwners.PlanningInterval.Key;

    /// <inheritdoc/>
    protected override string ViewPermission { get; } = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PlanningIntervals);

    /// <inheritdoc/>
    protected override string ManagePermission { get; } = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.PlanningIntervals);

    /// <inheritdoc/>
    protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.PlanningIntervals.AnyAsync(p => p.Id == ownerId, cancellationToken);
}
