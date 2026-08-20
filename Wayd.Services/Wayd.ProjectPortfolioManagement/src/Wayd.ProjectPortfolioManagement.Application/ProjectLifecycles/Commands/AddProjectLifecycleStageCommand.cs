namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record AddProjectLifecycleStageCommand(
    Guid LifecycleId,
    string Name,
    string Description)
    : ICommand<Guid>;

public sealed class AddProjectLifecycleStageCommandValidator : AbstractValidator<AddProjectLifecycleStageCommand>
{
    public AddProjectLifecycleStageCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class AddProjectLifecycleStageCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<AddProjectLifecycleStageCommandHandler> logger)
    : ICommandHandler<AddProjectLifecycleStageCommand, Guid>
{
    private const string AppRequestName = nameof(AddProjectLifecycleStageCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<AddProjectLifecycleStageCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddProjectLifecycleStageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .Include(x => x.Stages)
                .FirstOrDefaultAsync(r => r.Id == request.LifecycleId, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.LifecycleId);
                return Result.Failure<Guid>("Project Lifecycle not found.");
            }

            var addResult = lifecycle.AddStage(request.Name, request.Description);
            if (addResult.IsFailure)
            {
                _logger.LogError("Unable to add stage to Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.LifecycleId, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stage {StageId} added to Project Lifecycle {ProjectLifecycleId}.", addResult.Value.Id, request.LifecycleId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
