using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

public sealed class StrategicInitiative : BaseAuditableEntity, IHasIdAndKey
{
    private readonly HashSet<RoleAssignment<StrategicInitiativeRole>> _roles = [];
    private readonly HashSet<StrategicInitiativeProject> _strategicInitiativeProjects = [];

    private StrategicInitiative() { }

    private StrategicInitiative(string name, string description, StrategicInitiativeStatus status, LocalDateRange dateRange, Guid portfolioId, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles = null)
    {
        Name = name;
        Description = description;
        Status = status;
        DateRange = dateRange;
        PortfolioId = portfolioId;

        _roles = roles?
            .SelectMany(r => r.Value
                .Select(e => new RoleAssignment<StrategicInitiativeRole>(Id, r.Key, e)))
            .ToHashSet()
            ?? [];
    }

    /// <summary>
    /// The unique key of the strategic initiative.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the strategic initiative.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A detailed explanation of what the strategic initiative aims to achieve.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The status of the strategic initiative.
    /// </summary>
    public StrategicInitiativeStatus Status { get; private set; }

    /// <summary>
    /// The date range for the strategic initiative.
    /// </summary>
    public LocalDateRange DateRange
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(DateRange));
    } = default!;

    /// <summary>
    /// The Id of the portfolio to which this strategic initiative belongs.
    /// </summary>
    public Guid PortfolioId { get; private set; }

    /// <summary>
    /// The portfolio to which this strategic initiative belongs.
    /// </summary>
    public ProjectPortfolio? Portfolio { get; private set; }

    /// <summary>
    /// The roles associated with the strategic initiative.
    /// </summary>
    public IReadOnlyCollection<RoleAssignment<StrategicInitiativeRole>> Roles => _roles;

    /// <summary>
    /// The projects associated with this strategic initiative.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeProject> StrategicInitiativeProjects => _strategicInitiativeProjects;

    /// <summary>
    /// Indicates if the strategic initiative is in a closed state.
    /// </summary>
    public bool IsClosed => Status is StrategicInitiativeStatus.Completed or StrategicInitiativeStatus.Canceled;

    /// Indicates whether the strategic initiative can be deleted.
    /// </summary>
    /// <returns></returns>
    public bool CanBeDeleted() => Status is StrategicInitiativeStatus.Proposed or StrategicInitiativeStatus.Approved;

    /// <summary>
    /// Updates the name and description of the strategic initiative.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result UpdateDetails(string name, string description, EventActor actor, Instant timestamp)
    {
        // Compared after assignment, never against the arguments: the setters trim.
        var before = new StrategicInitiativeDetails(Name, Description);

        Name = name;
        Description = description;

        var after = new StrategicInitiativeDetails(Name, Description);
        if (before != after)
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeDetailsUpdatedEvent(
                Id, Key, after.Name, after.Description, before, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Moves the strategic initiative's start and end dates.
    /// </summary>
    /// <param name="dateRange"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result UpdateTimeline(LocalDateRange dateRange, EventActor actor, Instant timestamp)
    {
        var previous = DateRange;

        DateRange = dateRange;

        if (!Equals(previous, DateRange))
        {
            var current = DateRange;
            AddKeyedDomainEvent(() => new StrategicInitiativeTimelineChangedEvent(
                Id, Key, previous, current, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Updates the roles for the strategic initiative.
    /// </summary>
    /// <param name="updatedRoles"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result UpdateRoles(Dictionary<StrategicInitiativeRole, HashSet<Guid>> updatedRoles, EventActor actor, Instant timestamp)
    {
        var before = RoleManager.ToRoleMap(_roles);

        var result = RoleManager.UpdateRoles(_roles, Id, updatedRoles);
        if (result.IsFailure)
        {
            return result;
        }

        var after = RoleManager.ToRoleMap(_roles);
        var (added, removed) = RoleManager.Diff(before, after);
        if (added.Length > 0 || removed.Length > 0)
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeRolesChangedEvent(
                Id, Key, added, removed, after, actor, timestamp));
        }

        return result;
    }

    #region Lifecycle

    /// <summary>
    /// Approves the strategic initiative.
    /// </summary>
    public Result Approve(EventActor actor, Instant timestamp)
    {
        if (Status != StrategicInitiativeStatus.Proposed)
        {
            return Result.Failure("Only proposed strategic initiatives can be approved.");
        }

        ChangeStatus(StrategicInitiativeStatus.Approved, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Activates the strategic initiative.
    /// </summary>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (Status != StrategicInitiativeStatus.Approved)
        {
            return Result.Failure("Only approved strategic initiatives can be activated.");
        }

        ChangeStatus(StrategicInitiativeStatus.Active, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Marks the strategic initiative as completed.
    /// </summary>
    public Result Complete(EventActor actor, Instant timestamp)
    {
        if (Status is not (StrategicInitiativeStatus.Active or StrategicInitiativeStatus.OnHold))
        {
            return Result.Failure("Only active strategic initiatives can be completed.");
        }

        ChangeStatus(StrategicInitiativeStatus.Completed, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Cancels the strategic initiative.
    /// </summary>
    public Result Cancel(EventActor actor, Instant timestamp)
    {
        if (Status is StrategicInitiativeStatus.Completed or StrategicInitiativeStatus.Canceled)
        {
            return Result.Failure("The strategic initiative is already completed or canceled.");
        }

        ChangeStatus(StrategicInitiativeStatus.Canceled, actor, timestamp);

        return Result.Success();
    }

    private void ChangeStatus(StrategicInitiativeStatus toStatus, EventActor actor, Instant timestamp)
    {
        var fromStatus = Status;

        Status = toStatus;

        AddKeyedDomainEvent(() => new StrategicInitiativeStatusChangedEvent(
            Id,
            Key,
            fromStatus.ToString(),
            LifecycleCategories<StrategicInitiativeStatus>.Of(fromStatus),
            toStatus.ToString(),
            LifecycleCategories<StrategicInitiativeStatus>.Of(toStatus),
            actor,
            timestamp));
    }

    #endregion Lifecycle

    #region Projects

    /// <summary>
    /// Manages the projects associated with the strategic initiative.
    /// </summary>
    /// <param name="projectIds"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    /// <returns></returns>
    public Result ManageProjects(IEnumerable<Guid> projectIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(projectIds, nameof(projectIds));

        if (IsClosed)
        {
            return Result.Failure("Projects cannot be added or removed for closed strategic initiatives.");
        }

        var projectIdSet = projectIds.ToHashSet();
        var existingProjectIds = _strategicInitiativeProjects.Select(p => p.ProjectId).ToHashSet();

        // Remove projects that are no longer in the provided list
        _strategicInitiativeProjects.RemoveWhere(p => !projectIdSet.Contains(p.ProjectId));

        // Add new projects that are not already associated
        var newProjects = projectIdSet.Except(existingProjectIds)
                                      .Select(id => StrategicInitiativeProject.Create(Id, id));
        _strategicInitiativeProjects.UnionWith(newProjects);

        // Sorted so the same change always produces the same payload.
        Guid[] added = [.. projectIdSet.Except(existingProjectIds).Order()];
        Guid[] removed = [.. existingProjectIds.Except(projectIdSet).Order()];
        if (added.Length > 0 || removed.Length > 0)
        {
            Guid[] after = [.. projectIdSet.Order()];
            AddKeyedDomainEvent(() => new StrategicInitiativeProjectsChangedEvent(Id, Key, added, removed, after, actor, timestamp));
        }

        return Result.Success();
    }

    #endregion Projects

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// An import creates an initiative and walks it to its status, adds its KPIs and links its projects before
    /// that save. The factory runs when the event is raised, so everything else it carries must be captured
    /// in locals by the caller — only Key may be read inside it.
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Creates a new strategic initiative.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="description"></param>
    /// <param name="dateRange"></param>
    /// <param name="portfolioId"></param>
    /// <param name="roles"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the initiative was created.</param>
    /// <returns></returns>
    internal static StrategicInitiative Create(string name, string description, LocalDateRange dateRange, Guid portfolioId, Dictionary<StrategicInitiativeRole, HashSet<Guid>>? roles, EventActor actor, Instant timestamp)
    {
        var initiative = new StrategicInitiative(name, description, StrategicInitiativeStatus.Proposed, dateRange, portfolioId, roles);

        // Captured now, not when the action runs: the event records the initiative as created, and an import
        // moves it on before the first save. Only Key waits for the save that assigns it.
        var createdName = initiative.Name;
        var createdDescription = initiative.Description;
        var createdStatus = (int)initiative.Status;
        var createdDateRange = initiative.DateRange;
        var createdRoles = RoleManager.ToRoleMap(initiative._roles);

        initiative.AddPostPersistenceAction(() => initiative.AddDomainEvent(new StrategicInitiativeCreatedEvent(
            portfolioId,
            initiative.Id,
            initiative.Key,
            createdName,
            createdDescription,
            createdStatus,
            createdDateRange,
            createdRoles,
            actor,
            timestamp)));

        return initiative;
    }
}
