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
            .InclusiveBetween(1, 52);

        // Iteration names are the prefix plus a sequence number, and an iteration name is capped at 128, as the
        // import's rule says.
        RuleFor(c => c.IterationPrefix)
            .MaximumLength(32);
    }

    public async Task<bool> BeUniquePlanningIntervalName(string name, CancellationToken cancellationToken)
    {
        // Trimmed as the name is when stored, or a leading space passes here and collides on the unique index.
        var trimmed = name.Trim();
        return await _planningDbContext.PlanningIntervals.AllAsync(x => x.Name != trimmed, cancellationToken);
    }
}

public sealed class CreatePlanningIntervalCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<CreatePlanningIntervalCommandHandler> logger) : ICommandHandler<CreatePlanningIntervalCommand, ObjectIdAndKey>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<CreatePlanningIntervalCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreatePlanningIntervalCommand request, CancellationToken cancellationToken)
    {
        var createResult = PlanningInterval.Create(
            request.Name,
            request.Description,
            request.DateRange,
            request.IterationWeeks,
            request.IterationPrefix,
            EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
            _dateTimeProvider.Now
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
}
