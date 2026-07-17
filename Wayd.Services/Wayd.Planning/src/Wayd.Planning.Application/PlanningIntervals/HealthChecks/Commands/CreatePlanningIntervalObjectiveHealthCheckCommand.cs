using Wayd.Common.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;

public sealed record CreatePlanningIntervalObjectiveHealthCheckCommand(
    Guid PlanningIntervalObjectiveId,
    HealthStatus Status,
    Instant Expiration,
    string? Note) : ICommand<Guid>;

public sealed class CreatePlanningIntervalObjectiveHealthCheckCommandValidator
    : CustomValidator<CreatePlanningIntervalObjectiveHealthCheckCommand>
{
    public CreatePlanningIntervalObjectiveHealthCheckCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PlanningIntervalObjectiveId)
            .NotEmpty();

        RuleFor(c => c.Status)
            .IsInEnum()
            .WithMessage("A valid health status must be selected.");

        RuleFor(c => c.Expiration)
            .NotEmpty()
            .GreaterThan(dateTimeProvider.Now)
            .WithMessage("The Expiration must be in the future.");

        RuleFor(c => c.Note)
            .MaximumLength(1024);
    }
}

public sealed class CreatePlanningIntervalObjectiveHealthCheckCommandHandler(
    IPlanningDbContext planningDbContext,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    ILogger<CreatePlanningIntervalObjectiveHealthCheckCommandHandler> logger)
    : ICommandHandler<CreatePlanningIntervalObjectiveHealthCheckCommand, Guid>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<CreatePlanningIntervalObjectiveHealthCheckCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreatePlanningIntervalObjectiveHealthCheckCommand request, CancellationToken cancellationToken)
    {
        Guid? employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return Result.Failure<Guid>("Unable to determine the current user's employee Id.");

        var objective = await _planningDbContext.PlanningIntervalObjectives
            .Include(o => o.HealthChecks)
            .FirstOrDefaultAsync(o => o.Id == request.PlanningIntervalObjectiveId, cancellationToken);

        if (objective is null)
        {
            _logger.LogWarning("Planning Interval Objective {ObjectiveId} not found.", request.PlanningIntervalObjectiveId);
            return Result.Failure<Guid>($"Planning Interval Objective {request.PlanningIntervalObjectiveId} not found.");
        }

        var addResult = objective.AddHealthCheck(request.Status, employeeId.Value, request.Expiration, request.Note, _dateTimeProvider.Now);
        if (addResult.IsFailure)
        {
            _logger.LogError("Unable to add health check to objective {ObjectiveId}.  Error: {Error}", request.PlanningIntervalObjectiveId, addResult.Error);
            return Result.Failure<Guid>(addResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(addResult.Value.Id);
    }
}
