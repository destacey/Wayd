using System.Linq.Expressions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Requests.Planning;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions;

/// <summary>
/// Resolves who is asking into what a <see cref="PokerSession"/> needs to decide what they may do: their employee,
/// whether they hold the poker Administer permission, which of the session's invited teams they belong to, and
/// whether they manage its host. The session itself makes the decision.
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

    /// <summary>
    /// Whether the employee manages the host, as the host's module decides. False for no host, and for a kind of
    /// host no module answers for.
    /// </summary>
    Task<bool> ManagesHost(PokerSessionHost? host, Guid employeeId, CancellationToken cancellationToken);
}

/// <summary>
/// The caller as a reader of poker sessions.
/// </summary>
/// <param name="EmployeeId">The reader's employee, or null when the account has none.</param>
/// <param name="IsAdministrator">Whether the reader holds the poker Administer permission.</param>
/// <param name="TeamIds">The invited teams the reader belongs to.</param>
/// <param name="IsReadOnly">The request's token can change nothing, so the reader manages nothing.</param>
/// <param name="ManagedHostIds">The hosts the reader manages, of every kind, whose sessions they facilitate; none when null.</param>
public sealed record PokerSessionReader(Guid? EmployeeId, bool IsAdministrator, IReadOnlyCollection<Guid> TeamIds, bool IsReadOnly,
    IReadOnlyCollection<Guid>? ManagedHostIds = null)
{
    /// <summary>The hosts the reader manages, of every kind, whose sessions they facilitate.</summary>
    public IReadOnlyCollection<Guid> ManagedHostIds { get; init; } = ManagedHostIds ?? [];

    /// <summary>The sessions the reader can see, as <see cref="PokerSession.CanAccess"/> decides.</summary>
    public Expression<Func<PokerSession, bool>> Filter =>
        PokerSession.AccessibleTo(EmployeeId, IsAdministrator, TeamIds, ManagedHostIds);

    /// <summary>
    /// Whether the reader manages a session with these facilitators and host, as <see cref="PokerSession.CanManage"/>
    /// decides; false for a read-only token, and for a reader with no employee, since every poker command needs one.
    /// </summary>
    /// <param name="hostId">The session's host, or null for a standalone session.</param>
    public bool CanManage(IEnumerable<Guid> facilitatorIds, Guid? hostId) =>
        !IsReadOnly && EmployeeId is { } id
        && (IsAdministrator || facilitatorIds.Contains(id) || (hostId is { } host && ManagedHostIds.Contains(host)));
}

/// <inheritdoc />
public sealed class PokerSessionAccess(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IUserService userService,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    IEnumerable<IPokerSessionHostAccess> hostAccess) : IPokerSessionAccess
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
    private readonly IReadOnlyList<IPokerSessionHostAccess> _hostAccess = [.. hostAccess];

    /// <inheritdoc />
    public async Task<PokerSessionActor> ResolveActor(PokerSession session, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isAdministrator = await _currentPrincipal.HasPermission(AdministratorPermission, cancellationToken);
        var teamIds = await TeamsWithMember([.. session.InvitedTeams.Select(t => t.TeamId)], employeeId.Value, cancellationToken);
        var managesHost = await ManagesHost(session.Host, employeeId.Value, cancellationToken);

        return new PokerSessionActor(employeeId.Value, _currentUser.GetUserId(), isAdministrator, teamIds, managesHost);
    }

    /// <inheritdoc />
    public async Task<PokerSessionReader> ResolveReader(CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var isAdministrator = await _currentPrincipal.HoldsPermission(AdministratorPermission, cancellationToken);

        IReadOnlyCollection<Guid> teamIds = [];
        IReadOnlyCollection<Guid> managedHostIds = [];
        if (employeeId is not null && !isAdministrator)
        {
            managedHostIds = await HostsManagedBy(employeeId.Value, cancellationToken);

            var invitedTeamIds = await _planningDbContext.PokerSessions
                .Where(s => s.Visibility == Visibility.Private)
                .SelectMany(s => s.InvitedTeams.Select(t => t.TeamId))
                .Distinct()
                .ToListAsync(cancellationToken);

            teamIds = [.. await TeamsWithMember(invitedTeamIds, employeeId.Value, cancellationToken)];
        }

        return new PokerSessionReader(employeeId, isAdministrator, teamIds, _currentPrincipal.IsReadOnly, managedHostIds);
    }

    /// <inheritdoc />
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
        var managesHost = await ManagesHost(session.Host, employeeId.Value, cancellationToken);

        return session.CanAccess(new PokerSessionActor(employeeId.Value, userId, isAdministrator, teamIds, managesHost));
    }

    /// <inheritdoc />
    public async Task<bool> ManagesHost(PokerSessionHost? host, Guid employeeId, CancellationToken cancellationToken)
    {
        if (host is null)
            return false;

        var access = _hostAccess.FirstOrDefault(a => a.Kind == host.Kind);
        return access is not null && (await access.HostsManagedBy(employeeId, cancellationToken)).Contains(host.Id);
    }

    private async Task<IReadOnlyCollection<Guid>> HostsManagedBy(Guid employeeId, CancellationToken cancellationToken)
    {
        List<Guid> hostIds = [];
        foreach (var access in _hostAccess)
            hostIds.AddRange(await access.HostsManagedBy(employeeId, cancellationToken));
        return hostIds;
    }

    private Task<IReadOnlySet<Guid>> TeamsWithMember(IReadOnlyCollection<Guid> teamIds, Guid employeeId, CancellationToken cancellationToken) =>
        _dispatcher.TeamsWithMember(teamIds, employeeId, _dateTimeProvider.Today, cancellationToken);
}
