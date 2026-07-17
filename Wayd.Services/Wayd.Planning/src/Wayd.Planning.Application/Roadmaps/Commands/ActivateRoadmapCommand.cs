using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record ActivateRoadmapCommand(Guid Id) : ICommand;

public sealed class ActivateRoadmapCommandValidator : AbstractValidator<ActivateRoadmapCommand>
{
    public ActivateRoadmapCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class ActivateRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<ActivateRoadmapCommandHandler> logger) : ICommandHandler<ActivateRoadmapCommand>
{
    private const string AppRequestName = nameof(ActivateRoadmapCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<ActivateRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateRoadmapCommand request, CancellationToken cancellationToken)
    {
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

            var activateResult = roadmap.Activate(_currentUserEmployeeId);
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
