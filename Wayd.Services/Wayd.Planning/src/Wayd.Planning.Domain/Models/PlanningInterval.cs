using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces;
using Wayd.Planning.Domain.Enums;
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
        // TODO generate a new Guid, rather than depend on the DB.  This can be used when creating new Iterations.
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

    /// <summary>Updates the specified name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="objectivesLocked">if set to <c>true</c> [objectives locked].</param>
    /// <returns></returns>
    public Result Update(string name, string? description, bool objectivesLocked)
    {
        Name = name;
        Description = description;
        ObjectivesLocked = objectivesLocked;

        return Result.Success();
    }

    /// <summary>
    /// Manages the PI dates and iterations.
    /// </summary>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    public Result ManageDates(LocalDateRange dateRange, List<UpsertPlanningIntervalIteration> iterations)
    {
        DateRange = dateRange;

        //TODO: we are currently allowing gaps in the date ranges, but we should not allow that

        // verify no duplicate names
        var iterationNames = iterations.Select(i => i.Name).ToList();
        if (iterationNames.Distinct().Count() != iterationNames.Count)
            return Result.Failure("Iteration names must be unique within the PI.");

        // verify iteration dates are within the PI date range and don't overlap
        foreach (var iteration in iterations)
        {
            if (iteration.DateRange.Start < DateRange.Start)
                return Result.Failure("Iteration date ranges cannot start before the Planning Interval date range.");
            if (iteration.DateRange.End > DateRange.End)
                return Result.Failure("Iteration date ranges cannot end after the Planning Interval date range.");

            if (Iterations.Where(i => i.Id != iteration.Id).Any(x => x.DateRange.Overlaps(iteration.DateRange)))
                return Result.Failure("Iteration date ranges cannot overlap.");
        }

        // remove any iterations that are not in the list
        var initialIterationIds = _iterations.Select(i => i.Id).ToList();
        var removedIterations = _iterations.Where(i => !iterations.Any(x => x.Id == i.Id)).ToList();
        foreach (var removedIteration in removedIterations)
        {
            var deleteResult = DeleteIteration(removedIteration.Id);
            if (deleteResult.IsFailure)
                return Result.Failure(deleteResult.Error);
        }

        // update existing iterations
        foreach (var iteration in iterations.Where(x => !x.IsNew))
        {
            var updateResult = UpdateIteration(iteration.Id!.Value, iteration.Name, iteration.Category, iteration.DateRange);
            if (updateResult.IsFailure)
                return Result.Failure(updateResult.Error);
        }

        // add new iterations
        foreach (var iteration in iterations.Where(x => x.IsNew))
        {
            var addResult = AddIteration(iteration.Name, iteration.Category, iteration.DateRange);
            if (addResult.IsFailure)
                return Result.Failure(addResult.Error);
        }

        return Result.Success();
    }

    /// <summary>Manages the planning interval teams.</summary>
    /// <param name="teamIds">The team ids.</param>
    /// <returns></returns>
    public Result ManageTeams(IEnumerable<Guid> teamIds)
    {
        Guard.Against.Null(teamIds, nameof(teamIds));

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

        var addedTeams = teamIds.Where(x => !_teams.Any(y => y.TeamId == x)).ToList();
        foreach (var addedTeam in addedTeams)
        {
            _teams.Add(new PlanningIntervalTeam(Id, addedTeam));
        }

        return Result.Success();
    }

    #region Iterations

    /// <summary>
    /// Auto-generates iterations if none exist.
    /// </summary>
    /// <param name="iterationWeeks">Specifies the default length of each iteration.  The length of final iteration will also depend on the planning interval end date.</param>
    /// <param name="iterationPrefix">By default each iteration is named based on its sequence.  Providing a prefix can reduce confusion with iterations in other planning intervals.</param>
    /// <returns></returns>
    public Result InitializeIterations(int iterationWeeks, string? iterationPrefix)
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

            var addIterationResult = AddIteration(iterationName, iterationCategory, new LocalDateRange(iterationStart, iterationEnd));
            if (addIterationResult.IsFailure)
                return Result.Failure(addIterationResult.Error);

            if (isLastIteration)
                break;

            iterationStart = iterationEnd.PlusDays(1);
            iterationCount++;
        }

        return Result.Success();
    }

    public Result AddIteration(string name, IterationCategory category, LocalDateRange dateRange)
    {
        if (Iterations.Any(x => x.Name == name))
            return Result.Failure("Iteration name already exists.");

        if (Iterations.Any(x => x.DateRange.Overlaps(dateRange)))
            return Result.Failure("Iteration date range overlaps with existing iteration date range.");

        if (dateRange.Start < DateRange.Start)
            return Result.Failure("Iteration date range cannot start before the Planning Interval date range.");

        if (dateRange.End > DateRange.End)
            return Result.Failure("Iteration date range cannot end after the Planning Interval date range.");

        var iteration = new PlanningIntervalIteration(Id, name, category, dateRange);
        _iterations.Add(iteration);

        return Result.Success();
    }

    private Result UpdateIteration(Guid iterationId, string name, IterationCategory category, LocalDateRange dateRange)
    {
        var existingIteration = _iterations.FirstOrDefault(x => x.Id == iterationId);
        if (existingIteration == null)
            return Result.Failure($"Iteration {iterationId} not found.");

        var updateResult = existingIteration.Update(name, category, dateRange);
        return updateResult.IsSuccess ? Result.Success() : Result.Failure(updateResult.Error);
    }

    private Result DeleteIteration(Guid iterationId)
    {
        var existingIteration = _iterations.FirstOrDefault(x => x.Id == iterationId);
        if (existingIteration == null)
            return Result.Failure($"Iteration {iterationId} not found.");

        _iterations.Remove(existingIteration);

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
    public Result MapSprintToIteration(Guid iterationId, Iteration sprint)
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
    public Result UnmapSprint(Guid sprintId)
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
    /// Synchronizes team sprint mappings to the desired state.
    /// This is a sync/replace operation that:
    /// - Maps sprints as specified in the dictionary
    /// - Unmaps sprints not included in the desired state
    /// - Ensures idempotency and proper validation
    /// </summary>
    /// <param name="teamId">The team whose sprints are being synchronized.</param>
    /// <param name="iterationSprintMappings">Dictionary where key is iteration ID and value is sprint ID (null to unmap).</param>
    /// <param name="sprints">Dictionary of available sprints keyed by ID.</param>
    /// <returns>A result indicating success or failure with an error message.</returns>
    public Result SyncTeamSprintMappings(Guid teamId, Dictionary<Guid, Guid?> iterationSprintMappings, Dictionary<Guid, Iteration> sprints)
    {
        Guard.Against.Null(iterationSprintMappings, nameof(iterationSprintMappings));
        Guard.Against.Null(sprints, nameof(sprints));

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
                    var unmapResult = UnmapSprint(sprintMapping.SprintId);
                    if (unmapResult.IsFailure)
                        return unmapResult;
                }
            }
            else
            {
                // Map the sprint to the iteration (domain handles all validation)
                if (!sprints.TryGetValue(sprintId.Value, out var sprint))
                    return Result.Failure($"Sprint {sprintId.Value} not found.");

                var mapResult = MapSprintToIteration(iterationId, sprint);
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
            var unmapResult = UnmapSprint(currentSprint.SprintId);
            if (unmapResult.IsFailure)
                return unmapResult;
        }

        return Result.Success();
    }

    #endregion Sprint Mappings

    #region Objectives

    /// <summary>Creates a PI objective for a team.</summary>
    public Result<PlanningIntervalObjective> CreateObjective(PlanningTeam team, string name, string? description, bool isStretch, LocalDate? startDate, LocalDate? targetDate, int? order)
    {
        try
        {
            if (!CanCreateObjectives())
                return Result.Failure<PlanningIntervalObjective>("Objectives are locked for this Planning Interval.");

            var objective = new PlanningIntervalObjective(Id, team.Id, name, description, ObjectiveTypeFor(team), isStretch, startDate, targetDate, order);
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
    public Result<PlanningIntervalObjective> ImportObjective(PlanningTeam team, string name, string? description, ObjectiveStatus status, double progress, bool isStretch, LocalDate? startDate, LocalDate? targetDate, Instant? closedDate, int? order)
    {
        try
        {
            if (!CanCreateObjectives())
                return Result.Failure<PlanningIntervalObjective>("Objectives are locked for this Planning Interval.");

            var objective = PlanningIntervalObjective.Import(Id, team.Id, name, description, ObjectiveTypeFor(team), status, progress, isStretch, startDate, targetDate, closedDate, order);
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
    public Result<PlanningIntervalObjective> UpdateObjective(Guid piObjectiveId, string name, string? description, ObjectiveStatus status, double progress, LocalDate? startDate, LocalDate? targetDate, bool isStretch, Instant timestamp)
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

            var updateResult = existingObjective.Update(name, description, status, progress, startDate, targetDate, isStretch, timestamp);
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
    public Result UpdateObjectivesOrder(IReadOnlyDictionary<Guid, int?> orders)
    {
        var missing = orders.Keys.Where(id => _objectives.All(o => o.Id != id)).ToList();
        if (missing.Count > 0)
            return Result.Failure($"Objectives not found in this Planning Interval: {string.Join(", ", missing)}.");

        foreach (var (id, order) in orders)
            _objectives.First(o => o.Id == id).UpdateOrder(order);

        return Result.Success();
    }

    private static PlanningIntervalObjectiveType ObjectiveTypeFor(PlanningTeam team)
        => team.Type == TeamType.Team
            ? PlanningIntervalObjectiveType.Team
            : PlanningIntervalObjectiveType.TeamOfTeams;

    public Result DeleteObjective(Guid piObjectiveId)
    {
        try
        {
            if (ObjectivesLocked)
                return Result.Failure("Objectives are locked for this Planning Interval.");

            var existingObjective = _objectives.FirstOrDefault(x => x.Id == piObjectiveId);
            if (existingObjective == null)
                return Result.Failure($"Planning Interval Objective {piObjectiveId} not found.");

            // TODO: deleting it here is not soft deleting it
            //_objectives.Remove(existingObjective);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    #endregion Objectives

    /// <summary>Creates the specified name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    /// <param name="dateRange">The date range.</param>
    /// <returns></returns>
    public static Result<PlanningInterval> Create(string name, string? description, LocalDateRange dateRange, int iterationWeeks, string? iterationPrefix)
    {
        var planningInterval = new PlanningInterval(name, description, dateRange);
        var result = planningInterval.InitializeIterations(iterationWeeks, iterationPrefix);

        return result.IsFailure
            ? Result.Failure<PlanningInterval>(result.Error)
            : planningInterval;
    }
}
