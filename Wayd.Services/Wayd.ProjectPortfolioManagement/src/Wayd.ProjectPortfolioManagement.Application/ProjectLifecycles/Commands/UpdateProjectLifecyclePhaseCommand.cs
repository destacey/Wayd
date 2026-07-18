namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record UpdateProjectLifecyclePhaseCommand(
    Guid LifecycleId,
    Guid PhaseId,
    string Name,
    string Description)
    : ICommand;

public sealed class UpdateProjectLifecyclePhaseCommandValidator : AbstractValidator<UpdateProjectLifecyclePhaseCommand>
{
    public UpdateProjectLifecyclePhaseCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.PhaseId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

public sealed class UpdateProjectLifecyclePhaseCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateProjectLifecyclePhaseCommandHandler> logger)
    : ICommandHandler<UpdateProjectLifecyclePhaseCommand>
{
    private const string AppRequestName = nameof(UpdateProjectLifecyclePhaseCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateProjectLifecyclePhaseCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProjectLifecyclePhaseCommand request, CancellationToken cancellationToken)
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

            var updateResult = lifecycle.UpdatePhase(request.PhaseId, request.Name, request.Description);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update phase {PhaseId} on Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.PhaseId, request.LifecycleId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Phase {PhaseId} updated on Project Lifecycle {ProjectLifecycleId}.", request.PhaseId, request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
