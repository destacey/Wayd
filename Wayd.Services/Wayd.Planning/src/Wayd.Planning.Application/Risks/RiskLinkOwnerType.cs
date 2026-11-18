using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Planning.Domain;

namespace Wayd.Planning.Application.Risks;

/// <summary>
/// Who may read and change a risk's links: the Risks View permission reads them, and the Update permission, which
/// edits the risk, changes them.
/// </summary>
public sealed class RiskLinkOwnerType(IPlanningDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : PermissionLinkOwnerType(currentPrincipal)
{
    private readonly IPlanningDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public override string OwnerType => PlanningLinkOwners.Risk.Key;

    /// <inheritdoc/>
    protected override string ViewPermission { get; } = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Risks);

    /// <inheritdoc/>
    protected override string ManagePermission { get; } = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Risks);

    /// <inheritdoc/>
    protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.Risks.AnyAsync(r => r.Id == ownerId, cancellationToken);
}
