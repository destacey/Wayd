using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios;

/// <summary>
/// Who may read and change a portfolio's links.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the portfolio needs: the Project Portfolios View permission.
/// </para>
/// <para>
/// <strong>Changing</strong> is editing the portfolio, by the rule <c>ProjectPortfolio.UpdateDetails</c> applies: the
/// Update permission, delivery leadership (Owner or Manager on the portfolio, or the PPM administrator) and a linked
/// employee, on a portfolio that is not archived.
/// </para>
/// </remarks>
public sealed class PortfolioLinkOwnerType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : ILinkOwnerType
{
    /// <summary>The permission reading a portfolio needs.</summary>
    public static readonly string ViewPermission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios);

    /// <summary>The permission editing a portfolio needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.ProjectPortfolios);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string OwnerType => PpmLinkOwners.Portfolio.Key;

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        var portfolio = await _dbContext.Portfolios
            .AsNoTracking()
            .Include(p => p.Roles)
            .FirstOrDefaultAsync(p => p.Id == ownerId, cancellationToken);
        if (portfolio is null)
            return null;

        if (portfolio.IsReadOnly || !await _currentPrincipal.HasPermission(UpdatePermission, cancellationToken))
            return new LinkOwnerAccess(false);

        // Null for a read-only token and for an account with no employee, neither of which can lead a portfolio.
        var (employeeId, isPpmAdministrator) = await _currentPrincipal.ResolveManagementHint(cancellationToken);
        if (employeeId is not { } id)
            return new LinkOwnerAccess(false);

        var actor = new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId());

        return new LinkOwnerAccess(portfolio.CanManagePortfolio(actor));
    }
}
