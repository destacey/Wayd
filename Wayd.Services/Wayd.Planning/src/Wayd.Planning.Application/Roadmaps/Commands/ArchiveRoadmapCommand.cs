using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record ArchiveRoadmapCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class ArchiveRoadmapCommandValidator : AbstractValidator<ArchiveRoadmapCommand>
{
    public ArchiveRoadmapCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<ArchiveRoadmapCommandHandler> logger) : ICommandHandler<ArchiveRoadmapCommand>
{
    private const string AppRequestName = nameof(ArchiveRoadmapCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<ArchiveRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveRoadmapCommand request, CancellationToken cancellationToken)
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

            var archiveResult = roadmap.Archive(currentUserEmployeeId.Value);
            if (archiveResult.IsFailure)
            {
                // Reset the entity
                await _planningDbContext.Entry(roadmap).ReloadAsync(cancellationToken);

                _logger.LogError("Unable to archive Roadmap {RoadmapId}.  Error message: {Error}", request.Id, archiveResult.Error);
                return Result.Failure(archiveResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Roadmap {RoadmapId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
