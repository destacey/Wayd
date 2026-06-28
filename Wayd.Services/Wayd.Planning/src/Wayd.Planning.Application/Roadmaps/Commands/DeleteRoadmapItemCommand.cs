using Ardalis.GuardClauses;

namespace Wayd.Planning.Application.Roadmaps.Commands;

public sealed record DeleteRoadmapItemCommand(Guid RoadmapId, Guid ActivityId) : ICommand;
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

internal sealed class DeleteRoadmapItemCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, ILogger<DeleteRoadmapItemCommandHandler> logger) : ICommandHandler<DeleteRoadmapItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());
    private readonly ILogger<DeleteRoadmapItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRoadmapItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var roadmap = await _planningDbContext.Roadmaps
                .Include(x => x.RoadmapManagers)
                .Include(x => x.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
                return Result.Failure("Roadmap not found");

            var deleteResult = roadmap.DeleteItem(request.ActivityId, _currentUserEmployeeId);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", request.GetType().Name, request, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;

            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);

            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}

