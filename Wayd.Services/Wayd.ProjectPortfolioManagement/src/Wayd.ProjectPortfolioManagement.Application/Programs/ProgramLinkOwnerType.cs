using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Programs;

/// <summary>
/// Who may read and change a program's links.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the program needs, by <see cref="ProgramReadRule"/>'s rule.
/// </para>
/// <para>
/// <strong>Changing</strong> is editing the program, by the rule <c>Program.UpdateDetails</c> applies: the Update
/// permission, delivery leadership (Owner or Manager on the program or its portfolio, or the PPM administrator) and a
/// linked employee.
/// </para>
/// </remarks>
public sealed class ProgramLinkOwnerType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : ILinkOwnerType
{
    /// <summary>The permission editing a program needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Programs);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string OwnerType => PpmLinkOwners.Program.Key;

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ProgramReadRule.ViewPermission, cancellationToken))
            return null;

        // The portfolio's roles must be loaded: without them the check silently finds no inherited leadership and
        // refuses the portfolio's leader.
        var program = await _dbContext.Programs
            .AsNoTracking()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == ownerId, cancellationToken);
        if (program is null)
            return null;

        if (!await _currentPrincipal.HasPermission(UpdatePermission, cancellationToken))
            return new LinkOwnerAccess(false);

        // Null for a read-only token and for an account with no employee, neither of which can lead a program.
        var (employeeId, isPpmAdministrator) = await _currentPrincipal.ResolveManagementHint(cancellationToken);
        if (employeeId is not { } id)
            return new LinkOwnerAccess(false);

        var actor = new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId());

        return new LinkOwnerAccess(program.CanManageProgram(actor, program.AncestryRoles()));
    }
}
