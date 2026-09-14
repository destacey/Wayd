using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Interfaces;
using Wayd.Planning.Domain.Models.Iterations;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningInterval : BaseSoftDeletableEntity, ILocalSchedule, INavigable
{
    private readonly List<PlanningIntervalTeam> _teams = [];
    private readonly List<PlanningIntervalIteration> _iterations = [];
    private readonly List<PlanningIntervalObjective> _objectives = [];
    private readonly List<PlanningIntervalIterationSprint> _iterationSprints = [];

    private PlanningInterval() { }

    private PlanningInterval(string name, string? description, LocalDateRange dateRange)
    {
        Name = name;
        Description = description;
        DateRange = dateRange;

        ObjectivesLocked = false;
    }

    /// <summary>
    /// The unique key of the Planning Interval.  This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the Planning Interval.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the Planning Interval.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The date range of the Planning Interval.
    /// </summary>
    public LocalDateRange DateRange
    {
        get;
        private set => field = Guard.Against.Null(value, nameof(DateRange));
    } = default!;

    /// <summary>
    /// A value indicating whether objectives are locked for this Planning Interval.
    /// </summary>
    public bool ObjectivesLocked { get; private set; } = false;

    /// <summary>
    /// The teams associated with this Planning Interval.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalTeam> Teams => _teams.AsReadOnly();

    /// <summary>
    /// The iterations within this Planning Interval.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalIteration> Iterations => _iterations.OrderBy(i => i.DateRange.Start).ToList().AsReadOnly();

    /// <summary>
    /// The objectives associated with this Planning Interval.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalObjective> Objectives => _objectives.AsReadOnly();

    /// <summary>
    /// The sprints mapped to iterations within this Planning Interval.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalIterationSprint> IterationSprints => _iterationSprints.AsReadOnly();

    public double? CalculatePredictability(LocalDate date, Guid? teamId = null)
    {
        if (StateOn(date) == IterationState.Future)
            return null;

        var objectives = _objectives.Where(o => o.Type == PlanningIntervalObjectiveType.Team);
        if (teamId.HasValue)
            objectives = _objectives.Where(o => o.TeamId == teamId.Value).ToList();

        if (!objectives.Any())
            return null;

        var nonstretchCount = objectives.Count(o => !o.IsStretch);
        if (nonstretchCount == 0) { return 0; }

        var completedCount = objectives.Count(o => o.Status == ObjectiveStatus.Completed);
        return completedCount >= nonstretchCount ? 100.0d : Math.Round(100 * ((double)completedCount / nonstretchCount), 2);
    }

    /// <summary>
    /// Determines whether this planning interval can create objectives.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this planning interval can create objectives; otherwise, <c>false</c>.
    /// </returns>
    public bool CanCreateObjectives()
    {
        return !ObjectivesLocked;
    }

    /// <summary>
    /// Gets a calendar with PI and iteration dates.
    /// </summary>
    /// <returns></returns>
    public PlanningIntervalCalendar GetCalendar()
    {
        return new PlanningIntervalCalendar(this, Iterations);
    }

    /// <summary>Iteration state on given date.</summary>
    /// <param name="date">The date.</param>
    /// <returns></returns>
    public IterationState StateOn(LocalDate date)
    {
        if (DateRange.IsPastOn(date)) { return IterationState.Completed; }
        if (DateRange.IsActiveOn(date)) { return IterationState.Active; }
        return IterationState.Future;
    }

    /// <summary>
    /// Updates the name and description, and locks or unlocks the objectives. Locking is a change of state in
    /// its own right, so it raises its own event.
    /// </summary>
    public Result Update(string name, string? description, bool objectivesLocked, EventActor actor, Instant timestamp)
    {
        var previousDetails = new PlanningIntervalDetails(Name, Description);
        var previousObjectivesLocked = ObjectivesLocked;

        Name = name;
        Description = description;
        ObjectivesLocked = objectivesLocked;

        // Compared after assignment because the text setters trim.
        var details = new PlanningIntervalDetails(Name, Description);
        if (details != previousDetails)
            AddKeyedDomainEvent(() => new PlanningIntervalDetailsUpdatedEvent(Id, Key, details.Name, details.Description, previousDetails, actor, timestamp));

        if (ObjectivesLocked != previousObjectivesLocked)
        {
            if (ObjectivesLocked)
                AddKeyedDomainEvent(() => new PlanningIntervalObjectivesLockedEvent(Id, Key, actor, timestamp));
            else
                AddKeyedDomainEvent(() => new PlanningIntervalObjectivesUnlockedEvent(Id, Key, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Manages the PI dates and iterations.
    /// </summary>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    public Result ManageDates(LocalDateRange dateRange, List<UpsertPlanningIntervalIteration> iterations, EventActor actor, Instant timestamp)
    {
        var previousDateRange = DateRange;
        var previousMappings = SprintMappings();

        //TODO: we are currently allowing gaps in the date ranges, but we should not allow that

        // verify no duplicate names
        var iterationNames = iterations.Select(i => i.Name).ToList();
        if (iterationNames.Distinct().Count() != iterationNames.Count)
            return Result.Failure("Iteration names must be unique within the PI.");

        // Checked against the incoming range, and nothing is assigned until they all pass: a rejected call
        // leaves the interval as it was.
        foreach (var iteration in iterations)
        {
            if (iteration.DateRange.Start < dateRange.Start)
                return Result.Failure("Iteration date ranges cannot start before the Planning Interval date range.");
            if (iteration.DateRange.End > dateRange.End)
                return Result.Failure("Iteration date ranges cannot end after the Planning Interval date range.");

            if (Iterations.Where(i => i.Id != iteration.Id).Any(x => x.DateRange.Overlaps(iteration.DateRange)))
                return Result.Failure("Iteration date ranges cannot overlap.");
        }

        DateRange = dateRange;

        var newDateRange = DateRange;
        if (!newDateRange.Equals(previousDateRange))
            AddKeyedDomainEvent(() => new PlanningIntervalDateRangeChangedEvent(Id, Key, previousDateRange, newDateRange, actor, timestamp));

        // remove any iterations that are not in the list
        var removedIterations = _iterations.Where(i => !iterations.Any(x => x.Id == i.Id)).ToList();
        foreach (var removedIteration in removedIterations)
        {
            var deleteResult = DeleteIteration(removedIteration.Id, actor, timestamp);
            if (deleteResult.IsFailure)
                return Result.Failure(deleteResult.Error);
        }

        // update existing iterations
        foreach (var iteration in iterations.Where(x => !x.IsNew))
        {
            var updateResult = UpdateIteration(iteration.Id!.Value, iteration.Name, iteration.Category, iteration.DateRange, actor, timestamp);
            if (updateResult.IsFailure)
                return Result.Failure(updateResult.Error);
        }

        // add new iterations
        foreach (var iteration in iterations.Where(x => x.IsNew))
        {
            var addResult = AddIteration(iteration.Name, iteration.Category, iteration.DateRange, actor, timestamp);
            if (addResult.IsFailure)
                return Result.Failure(addResult.Error);
        }

        RaiseSprintMappingsChanged(previousMappings, actor, timestamp);

        return Result.Success();
    }

    /// <summary>Manages the planning interval teams.</summary>
    /// <param name="teamIds">The team ids.</param>
    /// <returns></returns>
    public Result ManageTeams(IEnumerable<Guid> teamIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(teamIds, nameof(teamIds));

        var previousMappings = SprintMappings();

        var removedTeams = _teams.Where(x => !teamIds.Contains(x.TeamId)).ToList();
        foreach (var removedTeam in removedTeams)
        {
            _teams.Remove(removedTeam);

            // Remove sprint mappings for the removed team
            var removedTeamSprints = _iterationSprints
                .Where(s => s.Sprint?.TeamId == removedTeam.TeamId)
                .ToList();
            foreach (var sprint in removedTeamSprints)
            {
                _iterationSprints.Remove(sprint);
            }
        }

        var addedTeams = teamIds.Where(x => !_teams.Any(y => y.TeamId == x)).Distinct().ToList();
        foreach (var addedTeam in addedTeams)
        {
            _teams.Add(new PlanningIntervalTeam(Id, addedTeam));
        }

        if (removedTeams.Count > 0 || addedTeams.Count > 0)
        {
            Guid[] added = [.. addedTeams];
            Guid[] removed = [.. removedTeams.Select(t => t.TeamId)];
            Guid[] current = [.. _teams.Select(t => t.TeamId)];
            AddKeyedDomainEvent(() => new PlanningIntervalTeamsChangedEvent(Id, Key, added, removed, current, actor, timestamp));
        }

        RaiseSprintMappingsChanged(previousMappings, actor, timestamp);

        return Result.Success();
    }

    #region Iterations

    /// <summary>
    /// Auto-generates iterations if none exist.
    /// </summary>
    /// <param name="iterationWeeks">Specifies the default length of each iteration.  The length of final iteration will also depend on the planning interval end date.</param>
    /// <param name="iterationPrefix">By default each iteration is named based on its sequence.  Providing a prefix can reduce confusion with iterations in other planning intervals.</param>
    /// <returns></returns>
    public Result InitializeIterations(int iterationWeeks, string? iterationPrefix, EventActor actor, Instant timestamp)
    {
        return InitializeIterationsCore(iterationWeeks, iterationPrefix, iteration => RaiseIterationAdded(iteration, actor, timestamp));
    }

    private Result InitializeIterationsCore(int iterationWeeks, string? iterationPrefix, Action<PlanningIntervalIteration> added)
    {
        if (Iterations.Count != 0)
            return Result.Failure("Unable to generate new iterations for a Planning Interval that has iterations.");

        var iterationStart = DateRange.Start;
        var iterationCount = 1;
        var isLastIteration = false;
        while (true)
        {
            var iterationName = $"{iterationPrefix}{iterationCount}";
            var iterationEnd = iterationStart.PlusDays(iterationWeeks * 7 - 1);
            var iterationCategory = IterationCategory.Development;
            if (iterationEnd >= DateRange.End)
            {
                iterationEnd = DateRange.End;
                iterationCategory = IterationCategory.InnovationAndPlanning;
                isLastIteration = true;
            }

            var addIterationResult = AddIterationCore(iterationName, iterationCategory, new LocalDateRange(iterationStart, iterationEnd));
            if (addIterationResult.IsFailure)
                return Result.Failure(addIterationResult.Error);

            added(addIterationResult.Value);

            if (isLastIteration)
                break;

            iterationStart = iterationEnd.PlusDays(1);
            iterationCount++;
        }

        return Result.Success();
    }

    public Result AddIteration(string name, IterationCategory category, LocalDateRange dateRange, EventActor actor, Instant timestamp)
    {
        var result = AddIterationCore(name, category, dateRange);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        RaiseIterationAdded(result.Value, actor, timestamp);

        return Result.Success();
    }

    private Result<PlanningIntervalIteration> AddIterationCore(string name, IterationCategory category, LocalDateRange dateRange)
    {
        if (Iterations.Any(x => x.Name == name))
            return Result.Failure<PlanningIntervalIteration>("Iteration name already exists.");

        if (Iterations.Any(x => x.DateRange.Overlaps(dateRange)))
            return Result.Failure<PlanningIntervalIteration>("Iteration date range overlaps with existing iteration date range.");

        if (dateRange.Start < DateRange.Start)
            return Result.Failure<PlanningIntervalIteration>("Iteration date range cannot start before the Planning Interval date range.");

        if (dateRange.End > DateRange.End)
            return Result.Failure<PlanningIntervalIteration>("Iteration date range cannot end after the Planning Interval date range.");

        var iteration = new PlanningIntervalIteration(Id, name, category, dateRange);
        _iterations.Add(iteration);

        return Result.Success(iteration);
    }

    private void RaiseIterationAdded(PlanningIntervalIteration iteration, EventActor actor, Instant timestamp)
    {
        var iterationId = iteration.Id;
        var name = iteration.Name;
        var category = iteration.Category;
        var dateRange = iteration.DateRange;

        AddKeyedDomainEvent(() => new PlanningIntervalIterationAddedEvent(Id, Key, iterationId, name, category, dateRange, actor, timestamp));
    }

    private Result UpdateIteration(Guid iterationId, string name, IterationCategory category, LocalDateRange dateRange, EventActor actor, Instant timestamp)
    {
        var existingIteration = _iterations.FirstOrDefault(x => x.Id == iterationId);
        if (existingIteration == null)
            return Result.Failure($"Iteration {iterationId} not found.");

        var previousDetails = new PlanningIntervalIterationDetails(existingIteration.Name, existingIteration.Category);
        var previousDateRange = existingIteration.DateRange;

        var updateResult = existingIteration.Update(name, category, dateRange);
        if (updateResult.IsFailure)
            return Result.Failure(updateResult.Error);

        // Compared after the update because the name setter trims.
        var details = new PlanningIntervalIterationDetails(existingIteration.Name, existingIteration.Category);
        if (details != previousDetails)
            AddKeyedDomainEvent(() => new PlanningIntervalIterationDetailsUpdatedEvent(Id, Key, iterationId, details.Name, details.Category, previousDetails, actor, timestamp));

        var newDateRange = existingIteration.DateRange;
        if (!newDateRange.Equals(previousDateRange))
            AddKeyedDomainEvent(() => new PlanningIntervalIterationDateRangeChangedEvent(Id, Key, iterationId, previousDateRange, newDateRange, actor, timestamp));

        return Result.Success();
    }

    private Result DeleteIteration(Guid iterationId, EventActor actor, Instant timestamp)
    {
        var existingIteration = _iterations.FirstOrDefault(x => x.Id == iterationId);
        if (existingIteration == null)
            return Result.Failure($"Iteration {iterationId} not found.");

        _iterations.Remove(existingIteration);
        _iterationSprints.RemoveAll(s => s.PlanningIntervalIterationId == iterationId);

        var name = existingIteration.Name;
        AddKeyedDomainEvent(() => new PlanningIntervalIterationRemovedEvent(Id, Key, iterationId, name, actor, timestamp));

        return Result.Success();
    }

    #endregion Iterations

    #region Sprint Mappings

    /// <summary>
    /// Maps a sprint to an iteration within this Planning Interval.
    /// </summary>
    /// <param name="iterationId">The iteration ID within this PI.</param>
    /// <param name="sprint">The sprint entity to map.</param>
    /// <returns>A result indicating success or failure with an error message.</returns>
    public Result MapSprintToIteration(Guid iterationId, Iteration sprint, EventActor actor, Instant timestamp)
    {
        var previousMappings = SprintMappings();

        var result = MapSprintToIterationCore(iterationId, sprint);
        if (result.IsSuccess)
            RaiseSprintMappingsChanged(previousMappings, actor, timestamp);

        return result;
    }

    private Result MapSprintToIterationCore(Guid iterationId, Iteration sprint)
    {
        Guard.Against.Null(sprint, nameof(sprint));

        // Validate iteration exists
        var iteration = _iterations.FirstOrDefault(i => i.Id == iterationId);
        if (iteration is null)
            return Result.Failure($"Iteration {iterationId} not found in this Planning Interval.");

        // Validate sprint type
        if (sprint.Type != IterationType.Sprint)
            return Result.Failure("Only sprints of type Sprint can be mapped to iterations.");

        // Validate sprint belongs to a team in the PI
        if (!sprint.TeamId.HasValue || !_teams.Any(t => t.TeamId == sprint.TeamId.Value))
            return Result.Failure("The sprint must belong to a team that is part of this Planning Interval.");

        // Check if sprint is already mapped
        var existingMapping = _iterationSprints.FirstOrDefault(s => s.SprintId == sprint.Id);
        if (existingMapping is not null)
        {
            // If already mapped to this iteration, operation is idempotent - return success
            if (existingMapping.PlanningIntervalIterationId == iterationId)
                return Result.Success();

            // Sprint is mapped to a different iteration - unmap it and continue to map to new iteration
            _iterationSprints.Remove(existingMapping);
        }

        // If the team already has a different sprint mapped to this iteration, unmap it first
        // This ensures a team can only have one sprint per iteration (replace behavior)
        var teamSprintInIteration = _iterationSprints
            .Where(s => s.PlanningIntervalIterationId == iterationId && s.SprintId != sprint.Id)
            .FirstOrDefault(s => s.Sprint?.TeamId == sprint.TeamId);
        if (teamSprintInIteration is not null)
        {
            _iterationSprints.Remove(teamSprintInIteration);
        }

        // Add the mapping
        var mapping = new PlanningIntervalIterationSprint(Id, iterationId, sprint.Id);
        _iterationSprints.Add(mapping);

        return Result.Success();
    }

    /// <summary>
    /// Removes a sprint mapping from an iteration within this Planning Interval.
    /// </summary>
    /// <param name="sprintId">The sprint ID to unmap.</param>
    /// <returns>A result indicating success or failure with an error message.</returns>
    public Result UnmapSprint(Guid sprintId, EventActor actor, Instant timestamp)
    {
        var previousMappings = SprintMappings();

        var result = UnmapSprintCore(sprintId);
        if (result.IsSuccess)
            RaiseSprintMappingsChanged(previousMappings, actor, timestamp);

        return result;
    }

    private Result UnmapSprintCore(Guid sprintId)
    {
        var mapping = _iterationSprints.FirstOrDefault(s => s.SprintId == sprintId);
        if (mapping is null)
            return Result.Failure("Sprint mapping not found in this Planning Interval.");

        _iterationSprints.Remove(mapping);
        return Result.Success();
    }

    /// <summary>
    /// Gets all sprints mapped to a specific iteration.
    /// </summary>
    /// <param name="iterationId">The iteration ID.</param>
    /// <returns>A collection of sprint mappings for the specified iteration.</returns>
    public IReadOnlyCollection<PlanningIntervalIterationSprint> GetSprintsForIteration(Guid iterationId)
    {
        return _iterationSprints.Where(s => s.PlanningIntervalIterationId == iterationId).ToList().AsReadOnly();
    }

    /// <summary>
    /// Synchronizes team sprint mappings to the desired state, raising one event for the whole change.
    /// This is a sync/replace operation that:
    /// - Maps sprints as specified in the dictionary
    /// - Unmaps sprints not included in the desired state
    /// - Ensures idempotency and proper validation
    /// </summary>
    /// <param name="teamId">The team whose sprints are being synchronized.</param>
    /// <param name="iterationSprintMappings">Dictionary where key is iteration ID and value is sprint ID (null to unmap).</param>
    /// <param name="sprints">Dictionary of available sprints keyed by ID.</param>
    /// <returns>A result indicating success or failure with an error message.</returns>
    public Result SyncTeamSprintMappings(Guid teamId, Dictionary<Guid, Guid?> iterationSprintMappings, Dictionary<Guid, Iteration> sprints, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(iterationSprintMappings, nameof(iterationSprintMappings));
        Guard.Against.Null(sprints, nameof(sprints));

        var previousMappings = SprintMappings();

        // Process each mapping in the dictionary
        foreach (var (iterationId, sprintId) in iterationSprintMappings)
        {
            if (!sprintId.HasValue)
            {
                // Null value means unmap any existing team sprint from this iteration
                var teamSprintsInIteration = _iterationSprints
                    .Where(s => s.PlanningIntervalIterationId == iterationId &&
                               s.Sprint?.TeamId == teamId)
                    .ToList();

                foreach (var sprintMapping in teamSprintsInIteration)
                {
                    var unmapResult = UnmapSprintCore(sprintMapping.SprintId);
                    if (unmapResult.IsFailure)
                        return unmapResult;
                }
            }
            else
            {
                // Map the sprint to the iteration (domain handles all validation)
                if (!sprints.TryGetValue(sprintId.Value, out var sprint))
                    return Result.Failure($"Sprint {sprintId.Value} not found.");

                var mapResult = MapSprintToIterationCore(iterationId, sprint);
                if (mapResult.IsFailure)
                    return mapResult;
            }
        }

        // Unmap any team sprints that are currently mapped but not in the desired state
        var desiredSprintIds = iterationSprintMappings.Values
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var currentTeamSprints = _iterationSprints
            .Where(s => s.Sprint?.TeamId == teamId)
            .Where(s => !desiredSprintIds.Contains(s.SprintId))
            .ToList();

        foreach (var currentSprint in currentTeamSprints)
        {
            var unmapResult = UnmapSprintCore(currentSprint.SprintId);
            if (unmapResult.IsFailure)
                return unmapResult;
        }

        RaiseSprintMappingsChanged(previousMappings, actor, timestamp);

        return Result.Success();
    }

    private PlanningIntervalSprintMapping[] SprintMappings()
        => [.. _iterationSprints.Select(s => new PlanningIntervalSprintMapping(s.PlanningIntervalIterationId, s.SprintId))];

    /// <summary>
    /// Raises one event for the net change since <paramref name="previous"/>, if there was one. Compared as sets,
    /// so a mapping removed and put back within the same call is no change.
    /// </summary>
    private void RaiseSprintMappingsChanged(PlanningIntervalSprintMapping[] previous, EventActor actor, Instant timestamp)
    {
        var current = SprintMappings();

        PlanningIntervalSprintMapping[] added = [.. current.Except(previous)];
        PlanningIntervalSprintMapping[] removed = [.. previous.Except(current)];
        if (added.Length == 0 && removed.Length == 0)
            return;

        AddKeyedDomainEvent(() => new PlanningIntervalSprintMappingsChangedEvent(Id, Key, added, removed, current, actor, timestamp));
    }

    #endregion Sprint Mappings

    #region Objectives

    /// <summary>Creates a PI objective for a team.</summary>
    public Result<PlanningIntervalObjective> CreateObjective(PlanningTeam team, string name, string? description, bool isStretch, LocalDate? startDate, LocalDate? targetDate, int? order, EventActor actor, Instant timestamp)
    {
        try
        {
            if (!CanCreateObjectives())
                return Result.Failure<PlanningIntervalObjective>("Objectives are locked for this Planning Interval.");

            var objective = PlanningIntervalObjective.Create(Id, team.Id, name, description, ObjectiveTypeFor(team), isStretch, startDate, targetDate, order, actor, timestamp);
            _objectives.Add(objective);

            return Result.Success(objective);
        }
        catch (Exception ex)
        {
            return Result.Failure<PlanningIntervalObjective>(ex.ToString());
        }
    }

    /// <summary>
    /// Adds an objective whose status, progress and closed date are already known, as an import does.
    /// </summary>
    public Result<PlanningIntervalObjective> ImportObjective(PlanningTeam team, string name, string? description, ObjectiveStatus status, double progress, bool isStretch, LocalDate? startDate, LocalDate? targetDate, Instant? closedDate, int? order, EventActor actor, Instant timestamp)
    {
        try
        {
            if (!CanCreateObjectives())
                return Result.Failure<PlanningIntervalObjective>("Objectives are locked for this Planning Interval.");

            var objective = PlanningIntervalObjective.Import(Id, team.Id, name, description, ObjectiveTypeFor(team), status, progress, isStretch, startDate, targetDate, closedDate, order, actor, timestamp);
            _objectives.Add(objective);

            return Result.Success(objective);
        }
        catch (Exception ex)
        {
            return Result.Failure<PlanningIntervalObjective>(ex.ToString());
        }
    }

    /// <summary>
    /// Updates an objective. Once objectives are locked the name and stretch flag are frozen; the rest
    /// stays editable so progress can still be reported against the committed plan.
    /// </summary>
    public Result<PlanningIntervalObjective> UpdateObjective(Guid piObjectiveId, string name, string? description, ObjectiveStatus status, double progress, LocalDate? startDate, LocalDate? targetDate, bool isStretch, EventActor actor, Instant timestamp)
    {
        try
        {
            var existingObjective = _objectives.FirstOrDefault(x => x.Id == piObjectiveId);
            if (existingObjective == null)
                return Result.Failure<PlanningIntervalObjective>($"Objective {piObjectiveId} not found.");

            if (ObjectivesLocked)
            {
                name = existingObjective.Name;
                isStretch = existingObjective.IsStretch;
            }

            var updateResult = existingObjective.Update(name, description, status, progress, startDate, targetDate, isStretch, actor, timestamp);
            if (updateResult.IsFailure)
                return Result.Failure<PlanningIntervalObjective>(updateResult.Error);

            return Result.Success(existingObjective);
        }
        catch (Exception ex)
        {
            return Result.Failure<PlanningIntervalObjective>(ex.ToString());
        }
    }

    /// <summary>
    /// Reorders objectives. Every id must belong to this planning interval, or nothing changes.
    /// </summary>
    public Result UpdateObjectivesOrder(IReadOnlyDictionary<Guid, int?> orders, EventActor actor, Instant timestamp)
    {
        var missing = orders.Keys.Where(id => _objectives.All(o => o.Id != id)).ToList();
        if (missing.Count > 0)
            return Result.Failure($"Objectives not found in this Planning Interval: {string.Join(", ", missing)}.");

        foreach (var (id, order) in orders)
            _objectives.First(o => o.Id == id).UpdateOrder(order, actor, timestamp);

        return Result.Success();
    }

    private static PlanningIntervalObjectiveType ObjectiveTypeFor(PlanningTeam team)
        => team.Type == TeamType.Team
            ? PlanningIntervalObjectiveType.Team
            : PlanningIntervalObjectiveType.TeamOfTeams;

    /// <summary>
    /// Raises the objective's deletion. The caller removes it in the same save, which is what drains the event.
    /// </summary>
    public Result DeleteObjective(Guid piObjectiveId, EventActor actor, Instant timestamp)
    {
        try
        {
            if (ObjectivesLocked)
                return Result.Failure("Objectives are locked for this Planning Interval.");

            var existingObjective = _objectives.FirstOrDefault(x => x.Id == piObjectiveId);
            if (existingObjective == null)
                return Result.Failure($"Planning Interval Objective {piObjectiveId} not found.");

            existingObjective.Delete(actor, timestamp);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    #endregion Objectives

    /// <summary>
    /// Raises an event whose payload carries <see cref="Key"/>, which the first save assigns; a change made
    /// before it waits for the key. <paramref name="build"/> runs at that point, so capture what it reads.
    /// </summary>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Creates a planning interval and generates its iterations. The iterations are part of the creation, so
    /// they raise no events of their own.
    /// </summary>
    public static Result<PlanningInterval> Create(string name, string? description, LocalDateRange dateRange, int iterationWeeks, string? iterationPrefix, EventActor actor, Instant timestamp)
    {
        var planningInterval = new PlanningInterval(name, description, dateRange);
        var result = planningInterval.InitializeIterationsCore(iterationWeeks, iterationPrefix, _ => { });
        if (result.IsFailure)
            return Result.Failure<PlanningInterval>(result.Error);

        // Captured now, not when the action runs: the event records the interval as created, so a caller that
        // changes it before the first save cannot rewrite the creation. Only Key waits for that save.
        var createdName = planningInterval.Name;
        var createdDescription = planningInterval.Description;
        var createdDateRange = planningInterval.DateRange;
        var createdObjectivesLocked = planningInterval.ObjectivesLocked;
        PlanningIntervalIterationValues[] createdIterations = [.. planningInterval.Iterations
            .Select(i => new PlanningIntervalIterationValues(i.Id, i.Name, i.Category, i.DateRange))];
        Guid[] createdTeamIds = [.. planningInterval._teams.Select(t => t.TeamId)];
        var createdSprintMappings = planningInterval.SprintMappings();

        planningInterval.AddPostPersistenceAction(() => planningInterval.AddDomainEvent(new PlanningIntervalCreatedEvent(
            planningInterval.Id,
            planningInterval.Key,
            createdName,
            createdDescription,
            createdDateRange,
            createdObjectivesLocked,
            createdIterations,
            createdTeamIds,
            createdSprintMappings,
            actor,
            timestamp)));

        return planningInterval;
    }
}
