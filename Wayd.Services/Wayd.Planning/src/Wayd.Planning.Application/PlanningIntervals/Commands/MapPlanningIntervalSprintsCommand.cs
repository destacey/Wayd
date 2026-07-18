namespace Wayd.Planning.Application.PlanningIntervals.Commands;

/// <summary>
/// Command to synchronize team sprint mappings to Planning Interval iterations.
/// This is a sync/replace operation - it sets the complete desired state for the team's sprint mappings.
/// Any team sprints currently mapped but not included in the IterationSprintMappings will be unmapped.
/// </summary>
/// <param name="PlanningIntervalId">The Planning Interval ID.</param>
/// <param name="TeamId">The team whose sprints are being synchronized.</param>
/// <param name="IterationSprintMappings">
/// Dictionary representing the complete desired state where key is iteration ID and value is sprint ID.
/// - Non-null value: Maps the sprint to the iteration.
/// - Null value: Explicitly unmaps any sprint from that iteration for this team.
/// - Omitted iteration: No change to that iteration's mappings.
/// Team sprints mapped to iterations not included in this dictionary will be unmapped.
/// </param>
public sealed record MapPlanningIntervalSprintsCommand(
    Guid PlanningIntervalId,
    Guid TeamId,
    Dictionary<Guid, Guid?> IterationSprintMappings) : ICommand;

public sealed class MapPlanningIntervalSprintsCommandValidator : CustomValidator<MapPlanningIntervalSprintsCommand>
{
    private readonly IPlanningDbContext _planningDbContext;

    public MapPlanningIntervalSprintsCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PlanningIntervalId)
            .NotEmpty()
            .WithMessage("Planning Interval ID is required.");

        RuleFor(c => c.TeamId)
            .NotEmpty()
            .WithMessage("Team ID is required.");

        RuleFor(c => c.IterationSprintMappings)
            .NotNull()
            .WithMessage("Iteration sprint mappings are required.");

        RuleFor(c => c)
            .MustAsync(async (command, cancellationToken) => await TeamBelongsToPlanningInterval(command.PlanningIntervalId, command.TeamId, cancellationToken))
            .WithMessage("The team must be part of the Planning Interval.");

        RuleFor(c => c)
            .MustAsync(async (command, cancellationToken) => await IterationsBelongToPlanningInterval(command.PlanningIntervalId, command.IterationSprintMappings.Keys, cancellationToken))
            .WithMessage("All iterations must belong to the Planning Interval.");
    }

    private async Task<bool> TeamBelongsToPlanningInterval(Guid planningIntervalId, Guid teamId, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals
            .Where(pi => pi.Id == planningIntervalId)
            .SelectMany(pi => pi.Teams)
            .AnyAsync(t => t.TeamId == teamId, cancellationToken);
    }

    private async Task<bool> IterationsBelongToPlanningInterval(Guid planningIntervalId, IEnumerable<Guid> iterationIds, CancellationToken cancellationToken)
    {
        var piIterationIds = await _planningDbContext.PlanningIntervals
            .Where(pi => pi.Id == planningIntervalId)
            .SelectMany(pi => pi.Iterations)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        return iterationIds.All(id => piIterationIds.Contains(id));
    }
}

public sealed class MapPlanningIntervalSprintsCommandHandler(
IPlanningDbContext planningDbContext,
ILogger<MapPlanningIntervalSprintsCommandHandler> logger) : ICommandHandler<MapPlanningIntervalSprintsCommand>
{
    private const string AppRequestName = nameof(MapPlanningIntervalSprintsCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<MapPlanningIntervalSprintsCommandHandler> _logger = logger;

    public async Task<Result> Handle(MapPlanningIntervalSprintsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Load the Planning Interval with necessary relationships
            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(pi => pi.Teams)
                .Include(pi => pi.Iterations)
                .Include(pi => pi.IterationSprints)
                    .ThenInclude(its => its.Sprint)
                .SingleOrDefaultAsync(pi => pi.Id == request.PlanningIntervalId, cancellationToken);

            if (planningInterval is null)
            {
                _logger.LogWarning("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure($"Planning Interval {request.PlanningIntervalId} not found.");
            }

            // Verify team belongs to the PI
            if (!planningInterval.Teams.Any(t => t.TeamId == request.TeamId))
            {
                _logger.LogWarning("Team {TeamId} is not part of Planning Interval {PlanningIntervalId}.",
                    request.TeamId, request.PlanningIntervalId);
                return Result.Failure($"Team {request.TeamId} is not part of Planning Interval {request.PlanningIntervalId}.");
            }

            // Get all sprint IDs that need to be loaded (non-null values)
            var sprintIds = request.IterationSprintMappings.Values
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            // Load all requested sprints
            var sprints = await _planningDbContext.Iterations
                .Where(i => sprintIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i, cancellationToken);

            // Let the domain handle all business logic and validation
            var syncResult = planningInterval.SyncTeamSprintMappings(
                request.TeamId,
                request.IterationSprintMappings,
                sprints);

            if (syncResult.IsFailure)
            {
                _logger.LogWarning("Failed to sync sprints for team {TeamId} in Planning Interval {PlanningIntervalId}: {Error}",
                    request.TeamId, request.PlanningIntervalId, syncResult.Error);
                return syncResult;
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully synced sprints for team {TeamId} in Planning Interval {PlanningIntervalId}.",
                request.TeamId, request.PlanningIntervalId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
