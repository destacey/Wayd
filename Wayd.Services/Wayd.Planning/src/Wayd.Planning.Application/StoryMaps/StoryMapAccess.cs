using System.Linq.Expressions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Planning.Application.StoryMaps;

/// <summary>
/// Resolves who is asking into what a <see cref="StoryMap"/> needs to decide what they may do: their employee,
/// whether they hold the Story Maps Administer permission, and which of the map's editor teams they belong to.
/// The map itself makes the decision.
/// </summary>
public interface IStoryMapAccess
{
    /// <summary>
    /// The caller as a <see cref="StoryMapActor"/> on <paramref name="map"/>, which must be loaded with
    /// <see cref="StoryMapQueryableExtensions.IncludeAccess"/>. Commands using this must be marked
    /// <c>IRequireLinkedEmployee</c>.
    /// </summary>
    /// <exception cref="ForbiddenException">Thrown via <c>LinkedEmployeeRequired</c> when the caller has no linked employee.</exception>
    Task<StoryMapActor> ResolveActor(StoryMap map, CancellationToken cancellationToken);

    /// <summary>
    /// The caller as a reader of maps: which they can see, edit and manage.
    /// </summary>
    Task<StoryMapReader> ResolveReader(CancellationToken cancellationToken);

    /// <summary>
    /// Whether a user, who need not be the caller, can see a map and receive its live updates: they hold the
    /// Story Maps View permission and the map admits them. False for a map that does not exist.
    /// </summary>
    Task<bool> CanUserAccess(Guid storyMapId, string userId, CancellationToken cancellationToken);
}

/// <summary>
/// The caller as a reader of story maps.
/// </summary>
/// <param name="EmployeeId">The reader's employee, or null when the account has none.</param>
/// <param name="IsAdministrator">Whether the reader holds the Story Maps Administer permission.</param>
/// <param name="TeamIds">The editor teams, on any map, the reader belongs to.</param>
/// <param name="IsReadOnly">The request's token can change nothing, so the reader edits and manages nothing.</param>
public sealed record StoryMapReader(Guid? EmployeeId, bool IsAdministrator, IReadOnlyCollection<Guid> TeamIds, bool IsReadOnly)
{
    /// <summary>The maps the reader can see, as <see cref="StoryMap.CanAccess"/> decides.</summary>
    public Expression<Func<StoryMap, bool>> Filter => StoryMap.AccessibleTo(EmployeeId, IsAdministrator, TeamIds);

    /// <summary>
    /// The reader as an actor, to ask a map loaded with <see cref="StoryMapQueryableExtensions.IncludeAccess"/>
    /// what they may do; null when they can change nothing, having no employee (every story map command needs one)
    /// or a read-only token.
    /// </summary>
    public StoryMapActor? Actor => !IsReadOnly && EmployeeId is { } id
        ? new StoryMapActor(id, IsAdministrator, TeamIds.ToHashSet())
        : null;
}

/// <inheritdoc />
public sealed class StoryMapAccess(
    IPlanningDbContext planningDbContext,
    ICurrentPrincipal currentPrincipal,
    IUserService userService,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider) : IStoryMapAccess
{
    /// <summary>Lets its holder see, edit and manage every story map.</summary>
    public static readonly string AdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.StoryMaps);

    /// <summary>Lets its holder into Story Maps; a map's visibility and editors decide which maps.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StoryMaps);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IUserService _userService = userService;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc />
    public async Task<StoryMapActor> ResolveActor(StoryMap map, CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isAdministrator = await _currentPrincipal.HasPermission(AdministratorPermission, cancellationToken);
        var teamIds = await TeamsWithMember([.. map.EditorTeams.Select(t => t.TeamId)], employeeId.Value, cancellationToken);

        return new StoryMapActor(employeeId.Value, isAdministrator, teamIds);
    }

    /// <inheritdoc />
    public async Task<StoryMapReader> ResolveReader(CancellationToken cancellationToken)
    {
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var isAdministrator = await _currentPrincipal.HoldsPermission(AdministratorPermission, cancellationToken);

        // Editor teams also decide who edits a public map, so every map's teams count, not only private ones'.
        IReadOnlyCollection<Guid> teamIds = [];
        if (employeeId is not null && !isAdministrator)
        {
            var editorTeamIds = await _planningDbContext.StoryMaps
                .SelectMany(m => m.EditorTeams.Select(t => t.TeamId))
                .Distinct()
                .ToListAsync(cancellationToken);

            teamIds = [.. await TeamsWithMember(editorTeamIds, employeeId.Value, cancellationToken)];
        }

        return new StoryMapReader(employeeId, isAdministrator, teamIds, _currentPrincipal.IsReadOnly);
    }

    /// <inheritdoc />
    public async Task<bool> CanUserAccess(Guid storyMapId, string userId, CancellationToken cancellationToken)
    {
        if (!await _userService.HasPermissionAsync(userId, ViewPermission, cancellationToken))
            return false;

        var map = await _planningDbContext.StoryMaps
            .AsNoTracking()
            .IncludeAccess()
            .FirstOrDefaultAsync(m => m.Id == storyMapId, cancellationToken);

        if (map is null)
            return false;

        if (map.Visibility == Visibility.Public)
            return true;

        if (await _userService.HasPermissionAsync(userId, AdministratorPermission, cancellationToken))
            return true;

        var employeeId = await _userService.GetEmployeeIdAsync(userId, cancellationToken);
        if (employeeId is null)
            return false;

        var teamIds = await TeamsWithMember([.. map.EditorTeams.Select(t => t.TeamId)], employeeId.Value, cancellationToken);

        return map.CanAccess(new StoryMapActor(employeeId.Value, false, teamIds));
    }

    private Task<IReadOnlySet<Guid>> TeamsWithMember(IReadOnlyCollection<Guid> teamIds, Guid employeeId, CancellationToken cancellationToken) =>
        _dispatcher.TeamsWithMember(teamIds, employeeId, _dateTimeProvider.Today, cancellationToken);
}
