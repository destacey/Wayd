using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record CreatePlanningIntervalObjectiveCommand(Guid PlanningIntervalId, Guid TeamId, string Name, string? Description, LocalDate? StartDate, LocalDate? TargetDate, bool IsStretch, int? Order) : ICommand<ObjectIdAndKey>;

public sealed class CreatePlanningIntervalObjectiveCommandValidator : CustomValidator<CreatePlanningIntervalObjectiveCommand>
{
    private readonly IPlanningDbContext _planningDbContext;

    public CreatePlanningIntervalObjectiveCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(o => o.Description)
            .MaximumLength(1024);

        When(o => o.StartDate.HasValue && o.TargetDate.HasValue, () =>
        {
            RuleFor(o => o.StartDate)
                .LessThan(o => o.TargetDate)
                .WithMessage("The start date must be before the target date.");
        });

        RuleFor(o => o.StartDate)
            .MustAsync(BeWithinPlanningIntervalDates)
            .WithMessage("The start date must be within the Planning Interval dates.");

        RuleFor(o => o.TargetDate)
            .MustAsync(BeWithinPlanningIntervalDates)
            .WithMessage("The target date must be within the Planning Interval dates.");
    }

    public async Task<bool> BeWithinPlanningIntervalDates(CreatePlanningIntervalObjectiveCommand command, LocalDate? date, CancellationToken cancellationToken)
    {
        if (!date.HasValue)
            return true;

        var planningInterval = await _planningDbContext.PlanningIntervals
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == command.PlanningIntervalId, cancellationToken);

        return planningInterval is null
            ? false
            : planningInterval.DateRange.Start <= date
                && date <= planningInterval.DateRange.End;
    }
}

public sealed class CreatePlanningIntervalObjectiveCommandHandler(IPlanningDbContext planningDbContext, ILogger<CreatePlanningIntervalObjectiveCommandHandler> logger) : ICommandHandler<CreatePlanningIntervalObjectiveCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreatePlanningIntervalObjectiveCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<CreatePlanningIntervalObjectiveCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreatePlanningIntervalObjectiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .FirstOrDefaultAsync(p => p.Id == request.PlanningIntervalId, cancellationToken);
            if (planningInterval is null)
            {
                _logger.LogError("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure<ObjectIdAndKey>("Planning Interval not found.");
            }

            if (planningInterval.ObjectivesLocked)
            {
                _logger.LogError("Objectives are locked for the Planning Interval {PlanningIntervalId}", request.PlanningIntervalId);
                return Result.Failure<ObjectIdAndKey>("Objectives are locked for the Planning Interval");
            }

            var team = await _planningDbContext.PlanningTeams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
            if (team is null)
            {
                _logger.LogError("Team {TeamId} not found.", request.TeamId);
                return Result.Failure<ObjectIdAndKey>("Team not found.");
            }

            var result = planningInterval.CreateObjective(team, request.Name, request.Description, request.IsStretch, request.StartDate, request.TargetDate, request.Order);
            if (result.IsFailure)
            {
                _logger.LogError("Unable to create PI objective for Planning Interval {PlanningIntervalId}.  Error: {Error}", request.PlanningIntervalId, result.Error);
                return Result.Failure<ObjectIdAndKey>($"Unable to create PI objective.  Error: {result.Error}");
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var piObjective = result.Value;

            _logger.LogInformation("Planning Interval Objective {PlanningIntervalObjectiveId} created with Key {PlanningIntervalObjectiveKey}.", piObjective.Id, piObjective.Key);

            return new ObjectIdAndKey(piObjective.Id, piObjective.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
