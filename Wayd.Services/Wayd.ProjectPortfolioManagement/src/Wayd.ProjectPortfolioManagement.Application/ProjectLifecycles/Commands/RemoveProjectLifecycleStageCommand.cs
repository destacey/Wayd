namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record RemoveProjectLifecycleStageCommand(
    Guid LifecycleId,
    Guid StageId)
    : ICommand;

public sealed class RemoveProjectLifecycleStageCommandValidator : AbstractValidator<RemoveProjectLifecycleStageCommand>
{
    public RemoveProjectLifecycleStageCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.StageId)
            .NotEmpty();
    }
}

public sealed class RemoveProjectLifecycleStageCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<RemoveProjectLifecycleStageCommandHandler> logger)
    : ICommandHandler<RemoveProjectLifecycleStageCommand>
{
    private const string AppRequestName = nameof(RemoveProjectLifecycleStageCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<RemoveProjectLifecycleStageCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveProjectLifecycleStageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .Include(x => x.Stages)
                .FirstOrDefaultAsync(r => r.Id == request.LifecycleId, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.LifecycleId);
                return Result.Failure("Project Lifecycle not found.");
            }

            var removeResult = lifecycle.RemoveStage(request.StageId);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove stage {StageId} from Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.StageId, request.LifecycleId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stage {StageId} removed from Project Lifecycle {ProjectLifecycleId}.", request.StageId, request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
