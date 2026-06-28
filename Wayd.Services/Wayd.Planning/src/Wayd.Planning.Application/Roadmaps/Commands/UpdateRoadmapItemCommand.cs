using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Planning.Application.Roadmaps.Commands;


public sealed record UpdateRoadmapItemCommand(Guid RoadmapId, Guid ItemId, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox> Item) : ICommand;

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

internal sealed class UpdateRoadmapItemCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapItemCommandHandler> logger) : ICommandHandler<UpdateRoadmapItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapItemCommand request, CancellationToken cancellationToken)
    {
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
               activity => roadmap.UpdateActivity(request.ItemId, activity, _currentUserEmployeeId),
               milestone => roadmap.UpdateMilestone(request.ItemId, milestone, _currentUserEmployeeId),
               timebox => roadmap.UpdateTimebox(request.ItemId, timebox, _currentUserEmployeeId)
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
