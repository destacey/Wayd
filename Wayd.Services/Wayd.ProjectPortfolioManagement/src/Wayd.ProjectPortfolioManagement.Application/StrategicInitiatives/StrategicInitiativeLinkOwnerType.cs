using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives;

/// <summary>
/// Who may read and change a strategic initiative's links: the Strategic Initiatives View permission reads them, and
/// the Update permission, which edits the initiative, changes them.
/// </summary>
/// <remarks>
/// A permission alone, unlike a project, program or portfolio: editing a strategic initiative needs no delivery
/// leadership, so neither do its links.
/// </remarks>
public sealed class StrategicInitiativeLinkOwnerType(IProjectPortfolioManagementDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : PermissionLinkOwnerType(currentPrincipal)
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public override string OwnerType => PpmLinkOwners.StrategicInitiative.Key;

    /// <inheritdoc/>
    protected override string ViewPermission { get; } = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StrategicInitiatives);

    /// <inheritdoc/>
    protected override string ManagePermission { get; } = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.StrategicInitiatives);

    /// <inheritdoc/>
    protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.StrategicInitiatives.AnyAsync(s => s.Id == ownerId, cancellationToken);
}
