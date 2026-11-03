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
        // Outside the try: this is a refusal, and the catch-all below would turn it into a
        // generic failure, losing both the 403 and its explanation.
        var caller = await _currentPrincipal.ResolveRoadmapActor(cancellationToken);

        try
        {
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

