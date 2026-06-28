using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using OneOf;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapItemDatesCommand(Guid RoadmapId, Guid ItemId, OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange> Dates) : ICommand;


public sealed class UpdateRoadmapItemDatesCommandValidator : AbstractValidator<UpdateRoadmapItemDatesCommand>
{
    private readonly IValidator<IUpsertRoadmapActivityDateRange> _activityValidator;
    private readonly IValidator<IUpsertRoadmapMilestoneDate> _milestoneValidator;
    private readonly IValidator<IUpsertRoadmapTimeboxDateRange> _timeboxValidator;

    public UpdateRoadmapItemDatesCommandValidator(
        IValidator<IUpsertRoadmapActivityDateRange> activityValidator,
        IValidator<IUpsertRoadmapMilestoneDate> milestoneValidator,
        IValidator<IUpsertRoadmapTimeboxDateRange> timeboxValidator)
    {
        _activityValidator = activityValidator;
        _milestoneValidator = milestoneValidator;
        _timeboxValidator = timeboxValidator;

        RuleFor(x => x.RoadmapId)
            .NotEmpty();

        RuleFor(x => x.ItemId)
            .NotEmpty();

        RuleFor(x => x.Dates)
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

    private static void ValidateWithValidator<T>(T item, IValidator<T> validator, ValidationContext<UpdateRoadmapItemDatesCommand> context)
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

internal sealed class UpdateRoadmapItemDatesCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapItemDatesCommandHandler> logger) : ICommandHandler<UpdateRoadmapItemDatesCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapItemDatesCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapItemDatesCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapItemDatesCommand request, CancellationToken cancellationToken)
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

            var updateResult = roadmap.UpdateRoadmapItemDates(request.ItemId, request.Dates, _currentUserEmployeeId);
            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);
                roadmap.ClearDomainEvents();

                _logger.LogError("Unable to update roadmap item {ItemId} dates in roadmap {RoadmapId}.  Error message: {Error}", request.ItemId, request.RoadmapId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Roadmap item {ItemId} in roadmap {RoadmapId} dates updated.", request.ItemId, request.RoadmapId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

