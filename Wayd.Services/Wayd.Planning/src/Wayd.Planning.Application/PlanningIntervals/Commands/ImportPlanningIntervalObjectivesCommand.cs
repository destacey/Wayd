using Wayd.Common.Application.Requests.Goals.Commands;
using Wayd.Common.Domain.Enums.Goals;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Application.PlanningIntervals.Extensions;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record ImportPlanningIntervalObjectivesCommand : ICommand
{
    public ImportPlanningIntervalObjectivesCommand(IEnumerable<ImportPlanningIntervalObjectiveDto> objectives)
    {
        Objectives = objectives.ToList();
    }

    public List<ImportPlanningIntervalObjectiveDto> Objectives { get; }
}

public sealed class ImportPlanningIntervalObjectivesCommandValidator : CustomValidator<ImportPlanningIntervalObjectivesCommand>
{
    public ImportPlanningIntervalObjectivesCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.Objectives)
            .NotNull()
            .NotEmpty();

        RuleForEach(o => o.Objectives)
            .NotNull()
            .SetValidator(new ImportPlanningIntervalObjectiveDtoValidator());
    }
}

public sealed class ImportPlanningIntervalObjectivesCommandHandler(IPlanningDbContext planningDbContext, IDispatcher dispatcher, ILogger<ImportPlanningIntervalObjectivesCommandHandler> logger) : ICommandHandler<ImportPlanningIntervalObjectivesCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ILogger<ImportPlanningIntervalObjectivesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ImportPlanningIntervalObjectivesCommand request, CancellationToken cancellationToken)
    {
        // TODO: allow individual records to fail and return a list of errors

        try
        {
            var piId = request.Objectives.First().PlanningIntervalId;

            var planningInterval = await _planningDbContext.PlanningIntervals
                .SingleOrDefaultAsync(p => p.Id == piId, cancellationToken);
            if (planningInterval is null)
                return Result.Failure<int>($"Planning Interval {piId} not found.");

            if (planningInterval.ObjectivesLocked)
                return Result.Failure<int>($"Objectives are locked for Planning Interval {piId}");

            var teams = await _planningDbContext.PlanningTeams
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var importedObjective in request.Objectives)
            {
                var team = teams.FirstOrDefault(t => t.Id == importedObjective.TeamId);
                if (team is null)
                    return Result.Failure<int>($"Team {importedObjective.TeamId} not found. (Record Id: {importedObjective.ImportId})");

                var mappedStatus = importedObjective.Status.ToGoalObjectiveStatus();

                var objectiveResult = await _dispatcher.Send(new ImportObjectiveCommand(
                    importedObjective.Name,
                    importedObjective.Description,
                    ObjectiveType.PlanningInterval,
                    mappedStatus,
                    importedObjective.Progress,
                    importedObjective.TeamId,
                    importedObjective.PlanningIntervalId,
                    importedObjective.StartDate,
                    importedObjective.TargetDate,
                    importedObjective.ClosedDateUtc,
                    importedObjective.Order), cancellationToken);
                if (objectiveResult.IsFailure)
                    return Result.Failure<int>($"Unable to create objective. (Record Id: {importedObjective.ImportId}).  Error: {objectiveResult.Error}");

                var result = planningInterval.CreateObjective(team, objectiveResult.Value, importedObjective.IsStretch);
                if (result.IsFailure)
                {
                    var deleteResult = await _dispatcher.Send(new DeleteObjectiveCommand(objectiveResult.Value), cancellationToken);
                    if (deleteResult.IsFailure)
                        _logger.LogError("Unable to delete objective. (Import Id: {ImportId}).  Error: {Error}", importedObjective.ImportId, deleteResult.Error);

                    _logger.LogError("Unable to create PI objective. (Import Id: {ImportId}).  Error: {Error}", importedObjective.ImportId, result.Error);
                    return Result.Failure<int>($"Unable to PI create objective. (Record Id: {importedObjective.ImportId}).  Error: {result.Error}");
                }

                await _planningDbContext.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
