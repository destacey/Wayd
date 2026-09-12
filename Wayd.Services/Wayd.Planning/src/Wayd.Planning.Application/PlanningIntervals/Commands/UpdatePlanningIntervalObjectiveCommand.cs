using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record UpdatePlanningIntervalObjectiveCommand(Guid PlanningIntervalId, Guid PlanningIntervalObjectiveId, string Name, string? Description, ObjectiveStatus Status, double Progress, LocalDate? StartDate, LocalDate? TargetDate, bool IsStretch) : ICommand<int>;

public sealed class UpdatePlanningIntervalObjectiveCommandValidator : CustomValidator<UpdatePlanningIntervalObjectiveCommand>
{
    private readonly IPlanningDbContext _planningDbContext;
    public UpdatePlanningIntervalObjectiveCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.Name)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(o => o.Description)
            .MaximumLength(1024);

        RuleFor(o => o.Status)
            .IsInEnum()
            .WithMessage("A valid objective status must be selected.");

        RuleFor(o => o.Progress)
            .InclusiveBetween(0.0d, 100.0d)
            .WithMessage("The progress must be between 0 and 100.");

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

    public async Task<bool> BeWithinPlanningIntervalDates(UpdatePlanningIntervalObjectiveCommand command, LocalDate? date, CancellationToken cancellationToken)
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

public sealed class UpdatePlanningIntervalObjectiveCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdatePlanningIntervalObjectiveCommandHandler> logger) : ICommandHandler<UpdatePlanningIntervalObjectiveCommand, int>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdatePlanningIntervalObjectiveCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(UpdatePlanningIntervalObjectiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .Include(pi => pi.Objectives.Where(o => o.Id == request.PlanningIntervalObjectiveId))
                .FirstOrDefaultAsync(p => p.Id == request.PlanningIntervalId, cancellationToken);

            if (planningInterval is null)
            {
                _logger.LogWarning("Planning Interval {PlanningIntervalId} not found.", request.PlanningIntervalId);
                return Result.Failure<int>($"Planning Interval {request.PlanningIntervalId} not found.");
            }

            var updateResult = planningInterval.UpdateObjective(
                request.PlanningIntervalObjectiveId,
                request.Name,
                request.Description,
                request.Status,
                request.Progress,
                request.StartDate,
                request.TargetDate,
                request.IsStretch,
                _dateTimeProvider.Now);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update PI objective {PlanningIntervalObjectiveId}.  Error: {Error}", request.PlanningIntervalObjectiveId, updateResult.Error);
                return Result.Failure<int>($"Unable to update PI objective.  Error: {updateResult.Error}");
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(updateResult.Value.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
