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
    private readonly HashSet<StrategicInitiativeKpi> _kpis = [];
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
    /// The KPIs associated with this strategic initiative.
    /// </summary>
    public IReadOnlyCollection<StrategicInitiativeKpi> Kpis => _kpis;

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

    #region KPIs

    /// <summary>
    /// Creates a new KPI for the strategic initiative.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    /// <returns></returns>
    public Result<StrategicInitiativeKpi> CreateKpi(StrategicInitiativeKpiUpsertParameters parameters, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(parameters, nameof(parameters));

        if (IsClosed)
        {
            return Result.Failure<StrategicInitiativeKpi>("KPIs cannot be created for closed strategic initiatives.");
        }

        var nextOrder = _kpis.Count == 0 ? 1 : _kpis.Max(k => k.Order) + 1;

        var kpi = StrategicInitiativeKpi.Create(Id, parameters, nextOrder);

        _kpis.Add(kpi);

        var (name, description, startingValue, targetValue, prefix, suffix, direction, order) =
            (kpi.Name, kpi.Description, kpi.StartingValue, kpi.TargetValue, kpi.Prefix, kpi.Suffix, kpi.TargetDirection, kpi.Order);

        AddKeyedDomainEvent(() => new StrategicInitiativeKpiAddedEvent(
            Id, Key, kpi.Id, name, description, startingValue, targetValue, prefix, suffix, direction, order, actor, timestamp));

        return kpi;
    }

    /// <summary>
    /// Updates an existing KPI for the strategic initiative.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="parameters"></param>
    /// <param name="actor">Who is making the change, for the domain events this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    /// <returns></returns>
    public Result UpdateKpi(Guid kpiId, StrategicInitiativeKpiUpsertParameters parameters, EventActor actor, Instant timestamp)
    {
        Guard.Against.NullOrEmpty(kpiId, nameof(kpiId));
        Guard.Against.Null(parameters, nameof(parameters));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be updated for closed strategic initiatives.");
        }

        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        var detailsBefore = KpiDetails(kpi);
        var targetBefore = KpiTarget(kpi);

        var result = kpi.Update(parameters);
        if (result.IsFailure)
        {
            return result;
        }

        // Compared after the update, never against the parameters: the setters trim and blank to null.
        var detailsAfter = KpiDetails(kpi);
        if (detailsBefore != detailsAfter)
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeKpiDetailsUpdatedEvent(
                Id, Key, kpi.Id, detailsAfter.Name, detailsAfter.Description, detailsAfter.Prefix, detailsAfter.Suffix,
                detailsBefore, actor, timestamp));
        }

        var targetAfter = KpiTarget(kpi);
        if (targetBefore != targetAfter)
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeKpiTargetChangedEvent(
                Id, Key, kpi.Id, targetAfter.StartingValue, targetAfter.TargetValue, targetAfter.TargetDirection,
                targetBefore, actor, timestamp));
        }

        return result;
    }

    private static StrategicInitiativeKpiDetails KpiDetails(StrategicInitiativeKpi kpi) =>
        new(kpi.Name, kpi.Description, kpi.Prefix, kpi.Suffix);

    private static StrategicInitiativeKpiTarget KpiTarget(StrategicInitiativeKpi kpi) =>
        new(kpi.StartingValue, kpi.TargetValue, kpi.TargetDirection);

    /// <summary>
    /// Deletes a KPI from the strategic initiative.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    /// <returns></returns>
    public Result DeleteKpi(Guid kpiId, EventActor actor, Instant timestamp)
    {
        Guard.Against.NullOrEmpty(kpiId, nameof(kpiId));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be deleted for closed strategic initiatives.");
        }

        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        _kpis.Remove(kpi);

        ResequenceKpiOrder();

        var name = kpi.Name;
        AddKeyedDomainEvent(() => new StrategicInitiativeKpiRemovedEvent(Id, Key, kpiId, name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Reorders the KPIs based on the provided ordered list of KPI IDs.
    /// </summary>
    /// <param name="orderedKpiIds">The KPI IDs in the desired order.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result ReorderKpis(List<Guid> orderedKpiIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(orderedKpiIds, nameof(orderedKpiIds));

        if (IsClosed)
        {
            return Result.Failure("KPIs cannot be reordered for closed strategic initiatives.");
        }

        if (orderedKpiIds.Count != _kpis.Count)
        {
            return Result.Failure("The number of KPI IDs must match the number of existing KPIs.");
        }

        if (orderedKpiIds.Distinct().Count() != orderedKpiIds.Count)
        {
            return Result.Failure("Duplicate KPI IDs are not allowed.");
        }

        var previousOrder = KpiOrder();

        for (int i = 0; i < orderedKpiIds.Count; i++)
        {
            var kpi = _kpis.FirstOrDefault(k => k.Id == orderedKpiIds[i]);
            if (kpi is null)
            {
                return Result.Failure($"KPI with ID '{orderedKpiIds[i]}' not found.");
            }

            kpi.Order = i + 1;
        }

        var order = KpiOrder();
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeKpisReorderedEvent(Id, Key, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    private Guid[] KpiOrder() => [.. _kpis.OrderBy(k => k.Order).Select(k => k.Id)];

    /// <summary>
    /// Replaces a KPI's checkpoint plan.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="checkpoints">The whole plan: checkpoints without an id are added, and existing ones left out are removed.</param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result ManageKpiCheckpointPlan(Guid kpiId, IEnumerable<UpsertStrategicInitiativeKpiCheckpoint> checkpoints, EventActor actor, Instant timestamp)
    {
        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        var before = CheckpointPlan(kpi).ToDictionary(c => c.CheckpointId);

        var result = kpi.ManageCheckpointPlan(checkpoints);
        if (result.IsFailure)
        {
            return result;
        }

        var after = CheckpointPlan(kpi);

        StrategicInitiativeKpiCheckpointValues[] added = [.. after.Where(c => !before.ContainsKey(c.CheckpointId))];
        StrategicInitiativeKpiCheckpointValues[] removed = [.. before.Values
            .Where(c => after.All(a => a.CheckpointId != c.CheckpointId))
            .OrderBy(c => c.CheckpointDate)];
        StrategicInitiativeKpiCheckpointRevision[] revised = [.. after
            .Where(c => before.TryGetValue(c.CheckpointId, out var previous) && previous != c)
            .Select(c => new StrategicInitiativeKpiCheckpointRevision(before[c.CheckpointId], c))];

        if (added.Length > 0 || removed.Length > 0 || revised.Length > 0)
        {
            AddKeyedDomainEvent(() => new StrategicInitiativeKpiCheckpointPlanChangedEvent(
                Id, Key, kpiId, added, removed, revised, after, actor, timestamp));
        }

        return result;
    }

    private static StrategicInitiativeKpiCheckpointValues[] CheckpointPlan(StrategicInitiativeKpi kpi) =>
        [.. kpi.Checkpoints
            .OrderBy(c => c.CheckpointDate)
            .Select(c => new StrategicInitiativeKpiCheckpointValues(c.Id, c.TargetValue, c.AtRiskValue, c.CheckpointDate, c.DateLabel))];

    /// <summary>
    /// Records a measurement against one of the strategic initiative's KPIs.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="measurement"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result AddKpiMeasurement(Guid kpiId, StrategicInitiativeKpiMeasurement measurement, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(measurement, nameof(measurement));

        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        var result = kpi.AddMeasurement(measurement);
        if (result.IsSuccess)
        {
            var (actualValue, measurementDate, measuredById, note) =
                (measurement.ActualValue, measurement.MeasurementDate, measurement.MeasuredById, measurement.Note);

            AddKeyedDomainEvent(() => new StrategicInitiativeKpiMeasurementAddedEvent(
                Id, Key, kpiId, measurement.Id, actualValue, measurementDate, measuredById, note, actor, timestamp));
        }

        return result;
    }

    /// <summary>
    /// Removes a measurement from one of the strategic initiative's KPIs.
    /// </summary>
    /// <param name="kpiId"></param>
    /// <param name="measurementId"></param>
    /// <param name="actor">Who is making the change, for the domain event this raises.</param>
    /// <param name="timestamp">The timestamp indicating when the change occurred.</param>
    public Result RemoveKpiMeasurement(Guid kpiId, Guid measurementId, EventActor actor, Instant timestamp)
    {
        var kpi = _kpis.FirstOrDefault(k => k.Id == kpiId);
        if (kpi is null)
        {
            return Result.Failure("KPI not found.");
        }

        var measurement = kpi.Measurements.FirstOrDefault(m => m.Id == measurementId);

        var result = kpi.RemoveMeasurement(measurementId);
        if (result.IsSuccess)
        {
            var (actualValue, measurementDate) = (measurement!.ActualValue, measurement.MeasurementDate);

            AddKeyedDomainEvent(() => new StrategicInitiativeKpiMeasurementRemovedEvent(
                Id, Key, kpiId, measurementId, actualValue, measurementDate, actor, timestamp));
        }

        return result;
    }

    /// <summary>
    /// Resets KPI ordering to eliminate gaps after removal.
    /// </summary>
    private void ResequenceKpiOrder()
    {
        int order = 1;
        foreach (var kpi in _kpis.OrderBy(k => k.Order))
        {
            kpi.Order = order;
            order++;
        }
    }

    #endregion KPIs

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
