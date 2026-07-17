using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record CreatePlanningIntervalCommand(string Name, string? Description, LocalDateRange DateRange, int IterationWeeks, string? IterationPrefix) : ICommand<ObjectIdAndKey>;

public sealed class CreatePlanningIntervalCommandValidator : CustomValidator<CreatePlanningIntervalCommand>
{
    private readonly IPlanningDbContext _planningDbContext;
    public CreatePlanningIntervalCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(BeUniquePlanningIntervalName).WithMessage("The Planning Interval name already exists.");

        RuleFor(c => c.Description)
            .MaximumLength(2048);

        RuleFor(c => c.DateRange)
            .NotNull();

        RuleFor(c => c.IterationWeeks)
            .GreaterThan(0);
    }

    public async Task<bool> BeUniquePlanningIntervalName(string name, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals.AllAsync(x => x.Name != name, cancellationToken);
    }
}

public sealed class CreatePlanningIntervalCommandHandler(IPlanningDbContext planningDbContext, ILogger<CreatePlanningIntervalCommandHandler> logger) : ICommandHandler<CreatePlanningIntervalCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreatePlanningIntervalCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<CreatePlanningIntervalCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreatePlanningIntervalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var createResult = PlanningInterval.Create(
                request.Name,
                request.Description,
                request.DateRange,
                request.IterationWeeks,
                request.IterationPrefix
                );
            if (createResult.IsFailure)
            {
                _logger.LogError("Error creating planning interval {ProjectName}. Error message: {Error}", request.Name, createResult.Error);
                return Result.Failure<ObjectIdAndKey>(createResult.Error);
            }

            var planningInterval = createResult.Value;

            await _planningDbContext.PlanningIntervals.AddAsync(planningInterval, cancellationToken);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Planning interval {PlanningIntervalId} created with Key {PlanningIntervalKey}.", planningInterval.Id, planningInterval.Key);

            return new ObjectIdAndKey(planningInterval.Id, planningInterval.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
