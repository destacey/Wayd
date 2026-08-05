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

public sealed class ActivateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<ActivateRoadmapCommandHandler> logger) : ICommandHandler<ActivateRoadmapCommand>
{
    private const string AppRequestName = nameof(ActivateRoadmapCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<ActivateRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateRoadmapCommand request, CancellationToken cancellationToken)
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
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (roadmap is null)
            {
                _logger.LogInformation("Roadmap {RoadmapId} not found.", request.Id);
                return Result.Failure("Roadmap not found.");
            }

            var activateResult = roadmap.Activate(currentUserEmployeeId.Value);
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);

                _logger.LogError("Unable to activate Roadmap {RoadmapId}.  Error message: {Error}", request.Id, activateResult.Error);
                return Result.Failure(activateResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Roadmap {RoadmapId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
