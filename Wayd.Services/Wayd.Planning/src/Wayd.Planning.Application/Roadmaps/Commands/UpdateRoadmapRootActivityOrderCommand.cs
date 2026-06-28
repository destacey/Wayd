using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapRootActivityOrderCommand(Guid RoadmapId, Guid RoadmapActivityId, int Order) : ICommand;

public sealed class UpdateRoadmapRootActivityOrderCommandValidator : CustomValidator<UpdateRoadmapRootActivityOrderCommand>
{
    public UpdateRoadmapRootActivityOrderCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(o => o.RoadmapId)
            .NotEmpty()
            .WithMessage("A valid roadmap id must be provided.");

        RuleFor(o => o.RoadmapActivityId)
            .NotEmpty()
            .WithMessage("A valid roadmap activity id must be provided.");

        RuleFor(o => o.Order)
            .GreaterThan(0)
            .WithMessage("Order must be greater than 0.");
    }
}

internal sealed class UpdateRoadmapRootActivityOrderCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<UpdateRoadmapRootActivityOrderCommandHandler> logger) : ICommandHandler<UpdateRoadmapRootActivityOrderCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapRootActivityOrderCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<UpdateRoadmapRootActivityOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapRootActivityOrderCommand request, CancellationToken cancellationToken)
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

            var updateResult = roadmap.SetActivityOrder(request.RoadmapActivityId, request.Order, _currentUserEmployeeId);
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


