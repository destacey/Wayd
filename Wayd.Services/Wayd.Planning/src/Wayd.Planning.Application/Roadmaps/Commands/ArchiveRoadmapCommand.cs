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

public sealed class ArchiveRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<ArchiveRoadmapCommandHandler> logger) : ICommandHandler<ArchiveRoadmapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ArchiveRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveRoadmapCommand request, CancellationToken cancellationToken)
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

        var archiveResult = roadmap.Archive(caller, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);
        if (archiveResult.IsFailure)
        {
            _logger.LogError("Unable to archive Roadmap {RoadmapId}.  Error message: {Error}", request.Id, archiveResult.Error);
            return Result.Failure(archiveResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Roadmap {RoadmapId} archived.", request.Id);

        return Result.Success();
    }
}
