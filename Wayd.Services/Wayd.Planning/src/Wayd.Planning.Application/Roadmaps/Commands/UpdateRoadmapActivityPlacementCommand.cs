using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapActivityPlacementCommand(Guid RoadmapId, Guid? ParentId, Guid ItemId, int Order) : ICommand;

public sealed class UpdateRoadmapActivityPlacementCommandValidator : CustomValidator<UpdateRoadmapActivityPlacementCommand>
{
    private readonly IPlanningDbContext _planningDbContext;

    public UpdateRoadmapActivityPlacementCommandValidator(IPlanningDbContext planningDbContext)
    {
        _planningDbContext = planningDbContext;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.RoadmapId)
            .NotEmpty()
            .WithMessage("A valid roadmap id must be provided.");

        When(o => o.ParentId.HasValue, () =>
        {
            RuleFor(o => o.ParentId)
                .NotEmpty()
                .WithMessage("A valid parent activity id must be provided.");
        });

        RuleFor(o => o.ItemId)
            .NotEmpty()
                .WithMessage("A valid activity id must be provided.")
            .MustAsync(BeActivityType)
                .WithMessage("The item id provided is not a valid activity.  Only roadmap activities can have their placement updated.");

        RuleFor(o => o.Order)
            .GreaterThan(0)
            .WithMessage("Order must be greater than 0.");
    }

    public async Task<bool> BeActivityType(UpdateRoadmapActivityPlacementCommand command, Guid itemId, CancellationToken cancellationToken)
    {
        var roadmap = await _planningDbContext.Roadmaps
            .Include(x => x.Items)
            .SingleOrDefaultAsync(r => r.Id == command.RoadmapId);

        if (roadmap is null)
            return false;

        var activity = roadmap.Items.OfType<RoadmapActivity>().FirstOrDefault(x => x.Id == itemId);

        return activity is not null;
    }
}

internal sealed class UpdateRoadmapActivityPlacementCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapActivityPlacementCommandHandler> logger) : ICommandHandler<UpdateRoadmapActivityPlacementCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapActivityPlacementCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapActivityPlacementCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapActivityPlacementCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .Include(x => x.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
            {
                _logger.LogDebug("Roadmap with id {RoadmapId} not found", request.RoadmapId);
                return Result.Failure($"Roadmap with id {request.RoadmapId} not found");
            }

            var activity = roadmap.Items.OfType<RoadmapActivity>().FirstOrDefault(x => x.Id == request.ItemId);
            if (activity is null)
            {
                _logger.LogDebug("Roadmap Activity with id {RoadmapActivityId} not found", request.ItemId);
                return Result.Failure($"Roadmap activity with id {request.ItemId} not found");
            }

            Result placementResult = Result.Success();
            if (activity.ParentId == request.ParentId)
            {
                placementResult = roadmap.SetActivityOrder(activity.Id, request.Order, _currentUserEmployeeId);
                _logger.LogInformation("Updated roadmap activity {RoadmapActivityId} order to {Order}.", request.ItemId, request.Order);
            }
            else
            {
                placementResult = roadmap.MoveActivity(activity.Id, request.ParentId, request.Order, _currentUserEmployeeId);
                _logger.LogInformation("Moved roadmap activity {RoadmapActivityId} to parent {ParentId} with order {Order}.", request.ItemId, request.ParentId, request.Order);
            }

            if (placementResult.IsFailure)
            {
                _logger.LogError("Unable to update roadmap activity placement {RoadmapActivityId} for request {@Request}.  Error message: {Error}", request.ItemId, request, placementResult.Error);
                return Result.Failure(placementResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
