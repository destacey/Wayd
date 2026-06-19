using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.HealthChecks.Commands;

public sealed record UpdatePlanningIntervalObjectiveHealthCheckCommand(
    Guid PlanningIntervalObjectiveId,
    Guid HealthCheckId,
    HealthStatus Status,
    Instant Expiration,
    string? Note) : ICommand<PlanningIntervalObjectiveHealthCheckDetailsDto>;

public sealed class UpdatePlanningIntervalObjectiveHealthCheckCommandValidator
    : CustomValidator<UpdatePlanningIntervalObjectiveHealthCheckCommand>
{
    public UpdatePlanningIntervalObjectiveHealthCheckCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PlanningIntervalObjectiveId)
            .NotEmpty();

        RuleFor(c => c.HealthCheckId)
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

internal sealed class UpdatePlanningIntervalObjectiveHealthCheckCommandHandler(
    IPlanningDbContext planningDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<UpdatePlanningIntervalObjectiveHealthCheckCommandHandler> logger)
    : ICommandHandler<UpdatePlanningIntervalObjectiveHealthCheckCommand, PlanningIntervalObjectiveHealthCheckDetailsDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdatePlanningIntervalObjectiveHealthCheckCommandHandler> _logger = logger;

    public async Task<Result<PlanningIntervalObjectiveHealthCheckDetailsDto>> Handle(UpdatePlanningIntervalObjectiveHealthCheckCommand request, CancellationToken cancellationToken)
    {
        var objective = await _planningDbContext.PlanningIntervalObjectives
            .Include(o => o.HealthChecks)
                .ThenInclude(h => h.ReportedBy)
            .FirstOrDefaultAsync(o => o.Id == request.PlanningIntervalObjectiveId, cancellationToken);

        if (objective is null)
        {
            _logger.LogWarning("Planning Interval Objective {ObjectiveId} not found.", request.PlanningIntervalObjectiveId);
            return Result.Failure<PlanningIntervalObjectiveHealthCheckDetailsDto>($"Planning Interval Objective {request.PlanningIntervalObjectiveId} not found.");
        }

        var updateResult = objective.UpdateHealthCheck(request.HealthCheckId, request.Status, request.Expiration, request.Note, _dateTimeProvider.Now);
        if (updateResult.IsFailure)
        {
            await _planningDbContext.Entry(objective).ReloadAsync(cancellationToken);
            objective.ClearDomainEvents();

            _logger.LogError("Unable to update health check {HealthCheckId} on objective {ObjectiveId}.  Error: {Error}", request.HealthCheckId, request.PlanningIntervalObjectiveId, updateResult.Error);
            return Result.Failure<PlanningIntervalObjectiveHealthCheckDetailsDto>(updateResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(updateResult.Value.Adapt<PlanningIntervalObjectiveHealthCheckDetailsDto>());
    }
}
