using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain;

namespace Wayd.Work.Application.Iterations.Sprints;

/// <summary>
/// Who may read and change a sprint's links.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the sprint needs: the Iterations View permission.
/// </para>
/// <para>
/// <strong>Changing</strong> follows every other change a person makes to a sprint, by
/// <see cref="SprintAuthorization.CanManageTeamSprints"/>: the Iterations Update permission and membership of the
/// sprint's team or its team of teams, which the Administer permission waives. A sprint whose team is not mapped to
/// a Wayd team takes no change from anyone, links included.
/// </para>
/// </remarks>
public sealed class SprintLinkOwnerType(
    IWorkDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IDispatcher dispatcher,
    ISystemCalendar systemCalendar) : ILinkOwnerType
{
    /// <summary>The permission reading a sprint needs.</summary>
    public static readonly string ViewPermission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Iterations);

    private readonly IWorkDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    /// <inheritdoc/>
    public string OwnerType => WorkLinkOwners.Sprint.Key;

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        var sprint = await _dbContext.Iterations
            .Where(i => i.Id == ownerId && i.Type == IterationType.Sprint)
            .Select(i => new { i.TeamId })
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null)
            return null;

        // A sprint with no Wayd team has no members to ask about, and no other sprint command accepts one.
        if (sprint.TeamId is not { } teamId)
            return new LinkOwnerAccess(false);

        return new LinkOwnerAccess(await _currentPrincipal.CanManageTeamSprints(
            _dispatcher, teamId, await _systemCalendar.Today(cancellationToken), cancellationToken));
    }
}
