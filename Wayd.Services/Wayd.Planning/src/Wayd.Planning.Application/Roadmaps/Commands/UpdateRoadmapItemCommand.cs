using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Planning.Application.Roadmaps.Commands;


public sealed record UpdateRoadmapItemCommand(Guid RoadmapId, Guid ItemId, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox> Item) : ICommand, IRequireLinkedEmployee;

public sealed class UpdateRoadmapItemCommandValidator : AbstractValidator<UpdateRoadmapItemCommand>
{
    private readonly IValidator<IUpsertRoadmapActivity> _activityValidator;
    private readonly IValidator<IUpsertRoadmapMilestone> _milestoneValidator;
    private readonly IValidator<IUpsertRoadmapTimebox> _timeboxValidator;

    public UpdateRoadmapItemCommandValidator(
        IValidator<IUpsertRoadmapActivity> activityValidator,
        IValidator<IUpsertRoadmapMilestone> milestoneValidator,
        IValidator<IUpsertRoadmapTimebox> timeboxValidator)
    {
        _activityValidator = activityValidator;
        _milestoneValidator = milestoneValidator;
        _timeboxValidator = timeboxValidator;

        RuleFor(x => x.RoadmapId)
            .NotEmpty();

        RuleFor(x => x.ItemId)
            .NotEmpty();

        RuleFor(x => x.Item)
            .NotNull()
            .Custom((item, context) =>
            {
                item.Switch(
                    activity => ValidateWithValidator(activity, _activityValidator, context),
                    milestone => ValidateWithValidator(milestone, _milestoneValidator, context),
                    timebox => ValidateWithValidator(timebox, _timeboxValidator, context)
                );
            });
    }

    private void ValidateWithValidator<T>(T item, IValidator<T> validator, ValidationContext<UpdateRoadmapItemCommand> context)
    {
        var validationResult = validator.Validate(item);
        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                context.AddFailure(error);
            }
        }
    }
}

public sealed class UpdateRoadmapItemCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<UpdateRoadmapItemCommandHandler> logger) : ICommandHandler<UpdateRoadmapItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<UpdateRoadmapItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapItemCommand request, CancellationToken cancellationToken)
    {
        // Outside the try: this is a refusal, and the catch-all below would turn it into a
        // generic failure, losing both the 403 and its explanation.
        var currentUserEmployeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (currentUserEmployeeId is null)
            LinkedEmployeeRequired.Throw();

        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .Include(x => x.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
                return Result.Failure<Guid>($"Roadmap with id {request.RoadmapId} not found");

            Result result = request.Item.Match(
               activity => roadmap.UpdateActivity(request.ItemId, activity, currentUserEmployeeId.Value),
               milestone => roadmap.UpdateMilestone(request.ItemId, milestone, currentUserEmployeeId.Value),
               timebox => roadmap.UpdateTimebox(request.ItemId, timebox, currentUserEmployeeId.Value)
            );

            if (result.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", request.GetType().Name, request, result.Error);
                return Result.Failure<Guid>(result.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure<Guid>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
