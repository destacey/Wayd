using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record ArchiveRoadmapCommand(Guid Id) : ICommand;

public sealed class ArchiveRoadmapCommandValidator : AbstractValidator<ArchiveRoadmapCommand>
{
    public ArchiveRoadmapCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

internal sealed class ArchiveRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<ArchiveRoadmapCommandHandler> logger) : ICommandHandler<ArchiveRoadmapCommand>
{
    private const string AppRequestName = nameof(ArchiveRoadmapCommand);

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<ArchiveRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveRoadmapCommand request, CancellationToken cancellationToken)
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

            var archiveResult = roadmap.Archive(_currentUserEmployeeId);
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
