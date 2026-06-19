namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record RemoveProjectLifecyclePhaseCommand(
    Guid LifecycleId,
    Guid PhaseId)
    : ICommand;

public sealed class RemoveProjectLifecyclePhaseCommandValidator : AbstractValidator<RemoveProjectLifecyclePhaseCommand>
{
    public RemoveProjectLifecyclePhaseCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.PhaseId)
            .NotEmpty();
    }
}

internal sealed class RemoveProjectLifecyclePhaseCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<RemoveProjectLifecyclePhaseCommandHandler> logger)
    : ICommandHandler<RemoveProjectLifecyclePhaseCommand>
{
    private const string AppRequestName = nameof(RemoveProjectLifecyclePhaseCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<RemoveProjectLifecyclePhaseCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveProjectLifecyclePhaseCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .Include(x => x.Phases)
                .FirstOrDefaultAsync(r => r.Id == request.LifecycleId, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.LifecycleId);
                return Result.Failure("Project Lifecycle not found.");
            }

            var removeResult = lifecycle.RemovePhase(request.PhaseId);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove phase {PhaseId} from Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.PhaseId, request.LifecycleId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Phase {PhaseId} removed from Project Lifecycle {ProjectLifecycleId}.", request.PhaseId, request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
