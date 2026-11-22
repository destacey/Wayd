using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record DeleteRoadmapItemCommand(Guid RoadmapId, Guid ActivityId) : ICommand, IRequireLinkedEmployee;
public sealed class DeleteRoadmapItemCommandValidator : AbstractValidator<DeleteRoadmapItemCommand>
{
    public DeleteRoadmapItemCommandValidator()
    {
        RuleFor(x => x.RoadmapId)
            .NotEmpty();

        RuleFor(x => x.ActivityId)
            .NotEmpty();
    }
}

public sealed class DeleteRoadmapItemCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteRoadmapItemCommandHandler> logger) : ICommandHandler<DeleteRoadmapItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteRoadmapItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRoadmapItemCommand request, CancellationToken cancellationToken)
    {
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        var roadmap = await _planningDbContext.Roadmaps
            .Include(x => x.Owners)
            .Include(x => x.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

        if (roadmap is null)
            return Result.Failure("Roadmap not found");

        var deleteResult = roadmap.DeleteItem(request.ActivityId, caller, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);
        if (deleteResult.IsFailure)
        {
            _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", request.GetType().Name, deleteResult.Error);
            return Result.Failure(deleteResult.Error);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

