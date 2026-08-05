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

public sealed class DeleteRoadmapItemCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<DeleteRoadmapItemCommandHandler> logger) : ICommandHandler<DeleteRoadmapItemCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteRoadmapItemCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRoadmapItemCommand request, CancellationToken cancellationToken)
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
                .Include(x => x.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == request.RoadmapId, cancellationToken);

            if (roadmap is null)
                return Result.Failure("Roadmap not found");

            var deleteResult = roadmap.DeleteItem(request.ActivityId, currentUserEmployeeId.Value);
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

