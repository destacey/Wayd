using System.Linq.Expressions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions;

/// <summary>
/// Resolves who is asking into what a <see cref="PokerSession"/> needs to decide what they may do: their employee,
/// whether they hold the poker Administer permission, and which of the session's invited teams they belong to.
/// The session itself makes the decision.
/// </summary>
public interface IPokerSessionAccess
{
    /// <summary>
    /// The caller as a <see cref="PokerSessionActor"/> on <paramref name="session"/>, which must be loaded with
    /// <see cref="PokerSessionQueryableExtensions.IncludeAccess"/>. Commands using this must be marked
    /// <c>IRequireLinkedEmployee</c>.
    /// </summary>
    /// <exception cref="ForbiddenException">Thrown via <c>LinkedEmployeeRequired</c> when the caller has no linked employee.</exception>
    Task<PokerSessionActor> ResolveActor(PokerSession session, CancellationToken cancellationToken);

    /// <summary>
    /// The caller as a reader of sessions: which they can see, and which they manage.
    /// </summary>
    Task<PokerSessionReader> ResolveReader(CancellationToken cancellationToken);

    /// <summary>
    /// Whether a user, who need not be the caller, can see a session and receive its live updates: they hold the
    /// poker View permission and the session admits them. False for a session that does not exist.
    /// </summary>
    Task<bool> CanUserAccess(Guid sessionId, string userId, CancellationToken cancellationToken);
}

/// <summary>
/// The caller as a reader of poker sessions.
/// </summary>
/// <param name="EmployeeId">The reader's employee, or null when the account has none.</param>
/// <param name="IsAdministrator">Whether the reader holds the poker Administer permission.</param>
/// <param name="TeamIds">The invited teams the reader belongs to.</param>
/// <param name="IsReadOnly">The request's token can change nothing, so the reader manages nothing.</param>
public sealed record PokerSessionReader(Guid? EmployeeId, bool IsAdministrator, IReadOnlyCollection<Guid> TeamIds, bool IsReadOnly)
{
    /// <summary>The sessions the reader can see, as <see cref="PokerSession.CanAccess"/> decides.</summary>
    public Expression<Func<PokerSession, bool>> Filter => PokerSession.AccessibleTo(EmployeeId, IsAdministrator, TeamIds);

    /// <summary>
    /// Whether the reader manages a session with these facilitators, as <see cref="PokerSession.CanManage"/>
    /// decides; false for a read-only token.
    /// </summary>
    public bool CanManage(IEnumerable<Guid> facilitatorIds) =>
        !IsReadOnly && (IsAdministrator || (EmployeeId is { } id && facilitatorIds.Contains(id)));
}

/// <inheritdoc />
public sealed class PokerSessionAccess(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IUserService userService,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider) : IPokerSessionAccess
{
    /// <summary>Lets its holder see and manage every poker session.</summary>
    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.PokerSessions);

    /// <summary>Lets its holder into Planning Poker; a session's visibility and invites decide which sessions.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PokerSessions);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUserService _userService = userService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<PokerSessionActor> ResolveActor(PokerSession session, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isAdministrator = await _currentPrincipal.HasPermission(AdministratorPermission, cancellationToken);
        var teamIds = await TeamsWithMember([.. session.InvitedTeams.Select(t => t.TeamId)], employeeId.Value, cancellationToken);

        return new PokerSessionActor(employeeId.Value, _currentUser.GetUserId(), isAdministrator, teamIds);
    }

    public async Task<PokerSessionReader> ResolveReader(CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var isAdministrator = await _currentPrincipal.HoldsPermission(AdministratorPermission, cancellationToken);

        IReadOnlyCollection<Guid> teamIds = [];
        if (employeeId is not null && !isAdministrator)
        {
            var invitedTeamIds = await _planningDbContext.PokerSessions
                .Where(s => s.Visibility == Visibility.Private)
                .SelectMany(s => s.InvitedTeams.Select(t => t.TeamId))
                .Distinct()
                .ToListAsync(cancellationToken);

            teamIds = [.. await TeamsWithMember(invitedTeamIds, employeeId.Value, cancellationToken)];
        }

        return new PokerSessionReader(employeeId, isAdministrator, teamIds, _currentPrincipal.IsReadOnly);
    }

    public async Task<bool> CanUserAccess(Guid sessionId, string userId, CancellationToken cancellationToken)
    {
        if (!await _userService.HasPermissionAsync(userId, ViewPermission, cancellationToken))
            return false;

        var session = await _planningDbContext.PokerSessions
            .AsNoTracking()
            .IncludeAccess()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return false;

        if (session.Visibility == Visibility.Public)
            return true;

        var employeeId = await _userService.GetEmployeeIdAsync(userId, cancellationToken);
        var isAdministrator = await _userService.HasPermissionAsync(userId, AdministratorPermission, cancellationToken);
        if (isAdministrator)
            return true;

        if (employeeId is null)
            return false;

        var teamIds = await TeamsWithMember([.. session.InvitedTeams.Select(t => t.TeamId)], employeeId.Value, cancellationToken);

        return session.CanAccess(new PokerSessionActor(employeeId.Value, userId, isAdministrator, teamIds));
    }

    private async Task<IReadOnlySet<Guid>> TeamsWithMember(IReadOnlyCollection<Guid> teamIds, Guid employeeId, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
            return new HashSet<Guid>();

        return await _dispatcher.Send(new GetTeamsWithMemberQuery(teamIds, employeeId, _dateTimeProvider.Today), cancellationToken);
    }
}
