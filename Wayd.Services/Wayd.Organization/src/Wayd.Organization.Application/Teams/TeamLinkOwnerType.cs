using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Organization.Domain;

namespace Wayd.Organization.Application.Teams;

/// <summary>
/// Who may read and change a team's or a team of teams' links: the Teams View permission reads them, and the Teams
/// Update permission, which edits the team, changes them.
/// </summary>
public sealed class TeamLinkOwnerType(IOrganizationDbContext dbContext, ICurrentPrincipal currentPrincipal)
    : PermissionLinkOwnerType(currentPrincipal)
{
    private readonly IOrganizationDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public override string OwnerType => OrganizationLinkOwners.Team.Key;

    /// <inheritdoc/>
    protected override string ViewPermission { get; } = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Teams);

    /// <inheritdoc/>
    protected override string ManagePermission { get; } = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Teams);

    /// <inheritdoc/>
    protected override Task<bool> Exists(Guid ownerId, CancellationToken cancellationToken) =>
        _dbContext.BaseTeams.AnyAsync(t => t.Id == ownerId, cancellationToken);
}
