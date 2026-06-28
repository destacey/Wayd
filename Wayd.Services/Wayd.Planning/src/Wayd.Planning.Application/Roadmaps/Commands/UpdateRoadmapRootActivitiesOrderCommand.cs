using Ardalis.GuardClauses;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapRootActivitiesOrderCommand(Guid RoadmapId, Dictionary<Guid, int> ChildrenOrder) : ICommand;

public sealed class UpdateRoadmapRootActivitiesOrderCommandValidator : CustomValidator<UpdateRoadmapRootActivitiesOrderCommand>
{
    public UpdateRoadmapRootActivitiesOrderCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.RoadmapId)
            .NotEmpty()
            .WithMessage("A roadmap must be selected.");

        RuleFor(o => o.ChildrenOrder)
            .NotEmpty()
            .WithMessage("At least one root roadmap activity must be provided.");
    }
}

internal sealed class UpdateRoadmapRootActivitiesOrderCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapRootActivitiesOrderCommandHandler> logger) : ICommandHandler<UpdateRoadmapRootActivitiesOrderCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapRootActivitiesOrderCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapRootActivitiesOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapRootActivitiesOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .Include(x => x.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
                return Result.Failure($"Roadmap with id {request.RoadmapId} not found");


            var rootActivities = roadmap.Items.OfType<RoadmapActivity>().Where(x => x.ParentId is null).ToList();
            if (rootActivities.Count != request.ChildrenOrder.Count)
            {
                var missingRoadmapActivities = request.ChildrenOrder.Keys.Except(rootActivities.Select(o => o.Id));
                _logger.LogWarning("Not all root roadmap activities provided were found for roadmap {RoadmapId}. The following root roadmap activities were not found: {RoadmapActivityIds}", roadmap.Id, missingRoadmapActivities);
                return Result.Failure("Not all root roadmap activities provided were found.");
            }

            var updateResult = roadmap.SetChildrenOrder(request.ChildrenOrder, _currentUserEmployeeId);
            if (updateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);
                roadmap.ClearDomainEvents();

                _logger.LogError("Failure for Request {CommandName} {@Request}.  Error message: {Error}", AppRequestName, request, updateResult.Error);
                return Result.Failure(updateResult.Error);
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


