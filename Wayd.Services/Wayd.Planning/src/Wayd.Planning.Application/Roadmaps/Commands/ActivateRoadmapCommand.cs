namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record ActivateRoadmapCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ActivateRoadmapCommandValidator : AbstractValidator<ActivateRoadmapCommand>
{
    public ActivateRoadmapCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class ActivateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<ActivateRoadmapCommandHandler> logger) : ICommandHandler<ActivateRoadmapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ActivateRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateRoadmapCommand request, CancellationToken cancellationToken)
    {
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        var roadmap = await _planningDbContext.Roadmaps
            .Include(x => x.Owners)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (roadmap is null)
        {
            _logger.LogInformation("Roadmap {RoadmapId} not found.", request.Id);
            return Result.Failure("Roadmap not found.");
        }

        var activateResult = roadmap.Activate(caller, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);
        if (activateResult.IsFailure)
        {
            _logger.LogError("Unable to activate Roadmap {RoadmapId}.  Error message: {Error}", request.Id, activateResult.Error);
            return Result.Failure(activateResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Roadmap {RoadmapId} activated.", request.Id);

        return Result.Success();
    }
}
