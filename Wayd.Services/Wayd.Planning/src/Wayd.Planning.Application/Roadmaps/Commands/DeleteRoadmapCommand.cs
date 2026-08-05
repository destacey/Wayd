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

public sealed class DeleteRoadmapCommandHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal, ILogger<DeleteRoadmapCommandHandler> logger) : ICommandHandler<DeleteRoadmapCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ILogger<DeleteRoadmapCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteRoadmapCommand request, CancellationToken cancellationToken)
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
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (roadmap is null)
                return Result.Failure($"Roadmap with id {request.Id} not found");

            var deleteResult = roadmap.CanDelete(currentUserEmployeeId.Value);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Wayd Request: Failure for Request {Name} {@Request}.  Error message: {Error}", request.GetType().Name, request, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            _planningDbContext.Roadmaps.Remove(roadmap);
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

