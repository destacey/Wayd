namespace Wayd.Planning.Application.PlanningIntervals.Commands;

public sealed record UpdatePlanningIntervalCommand(Guid Id, string Name, string? Description, bool ObjectivesLocked) : ICommand<int>;

public sealed class UpdatePlanningIntervalCommandValidator : CustomValidator<UpdatePlanningIntervalCommand>
{
    private readonly IPlanningDbContext _planningDbContext;
    public UpdatePlanningIntervalCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name)
            .NotEmpty()
            .MaximumLength(128)
            .MustAsync(async (model, name, cancellationToken)
                => await BeUniquePlanningIntervalName(model.Id, name, cancellationToken))
                .WithMessage("The Planning Interval name already exists.");

        RuleFor(e => e.Description)
            .MaximumLength(2048);
    }

    public async Task<bool> BeUniquePlanningIntervalName(Guid id, string name, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals.Where(t => t.Id != id).AllAsync(x => x.Name != name, cancellationToken);
    }
}

public sealed class UpdatePlanningIntervalCommandHandler : ICommandHandler<UpdatePlanningIntervalCommand, int>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdatePlanningIntervalCommandHandler> _logger;

    public UpdatePlanningIntervalCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdatePlanningIntervalCommandHandler> logger)
    {
        _planningDbContext = planningDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(UpdatePlanningIntervalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var planningInterval = await _planningDbContext.PlanningIntervals
                .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            if (planningInterval is null)
                return Result.Failure<int>("Planning Interval not found.");

            var updateResult = planningInterval.Update(
                request.Name,
                request.Description,
                request.ObjectivesLocked
                );

            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(planningInterval).ReloadAsync(cancellationToken);
                planningInterval.ClearDomainEvents();

                var requestName = request.GetType().Name;
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", requestName, request, updateResult.Error);
                return Result.Failure<int>(updateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(planningInterval.Key);
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<int>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

