using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record DeleteRoadmapCommand(Guid Id) : ICommand, IRequireLinkedEmployee;
public sealed class DeleteRoadmapCommandValidator : AbstractValidator<DeleteRoadmapCommand>
{
    public DeleteRoadmapCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteRoadmapCommandHandler> logger) : ICommandHandler<DeleteRoadmapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRoadmapCommand request, CancellationToken cancellationToken)
    {
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        var roadmap = await _planningDbContext.Roadmaps
            .Include(x => x.Owners)
            .Include(x => x.Items)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (roadmap is null)
            return Result.Failure($"Roadmap with id {request.Id} not found");

        var deleteResult = roadmap.Delete(caller, EventActor.User(_currentUser.GetUserId(), caller.EmployeeId), _dateTimeProvider.Now);
        if (deleteResult.IsFailure)
        {
            _logger.LogError("Wayd Request: Failure for Request {Name}.  Error message: {Error}", request.GetType().Name, deleteResult.Error);
            return Result.Failure(deleteResult.Error);
        }

        _planningDbContext.Roadmaps.Remove(roadmap);
        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

