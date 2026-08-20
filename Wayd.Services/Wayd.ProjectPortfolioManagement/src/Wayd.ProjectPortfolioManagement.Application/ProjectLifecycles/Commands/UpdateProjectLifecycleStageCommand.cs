namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record UpdateProjectLifecycleStageCommand(
    Guid LifecycleId,
    Guid StageId,
    string Name,
    string Description)
    : ICommand;

public sealed class UpdateProjectLifecycleStageCommandValidator : AbstractValidator<UpdateProjectLifecycleStageCommand>
{
    public UpdateProjectLifecycleStageCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.StageId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class UpdateProjectLifecycleStageCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateProjectLifecycleStageCommandHandler> logger)
    : ICommandHandler<UpdateProjectLifecycleStageCommand>
{
    private const string AppRequestName = nameof(UpdateProjectLifecycleStageCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateProjectLifecycleStageCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectLifecycleStageCommand request, CancellationToken cancellationToken)
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

            var updateResult = lifecycle.UpdateStage(request.StageId, request.Name, request.Description);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update stage {StageId} on Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.StageId, request.LifecycleId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stage {StageId} updated on Project Lifecycle {ProjectLifecycleId}.", request.StageId, request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
