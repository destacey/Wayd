using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record UpdateRoadmapRootActivityOrderCommand(Guid RoadmapId, Guid RoadmapActivityId, int Order) : ICommand, IRequireLinkedEmployee;

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

public sealed class UpdateRoadmapRootActivityOrderCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<UpdateRoadmapRootActivityOrderCommandHandler> logger) : ICommandHandler<UpdateRoadmapRootActivityOrderCommand>
{
    private const string AppRequestName = nameof(UpdateRoadmapRootActivityOrderCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<UpdateRoadmapRootActivityOrderCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateRoadmapRootActivityOrderCommand request, CancellationToken cancellationToken)
    {
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        var roadmap = await _planningDbContext.Roadmaps
            .Include(x => x.Owners)
            .Include(x => x.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

        if (roadmap is null)
            return Result.Failure($"Roadmap with id {request.RoadmapId} not found");

        var updateResult = roadmap.SetActivityOrder(request.RoadmapActivityId, request.Order, caller, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);
        if (updateResult.IsFailure)
        {
            _logger.LogError("Failure for Request {CommandName}.  Error message: {Error}", AppRequestName, updateResult.Error);
            return Result.Failure(updateResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}


