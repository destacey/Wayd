namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record ActivateProjectLifecycleCommand(Guid Id) : ICommand;

public sealed class ActivateProjectLifecycleCommandValidator : AbstractValidator<ActivateProjectLifecycleCommand>
{
    public ActivateProjectLifecycleCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

internal sealed class ActivateProjectLifecycleCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ActivateProjectLifecycleCommandHandler> logger)
    : ICommandHandler<ActivateProjectLifecycleCommand>
{
    private const string AppRequestName = nameof(ActivateProjectLifecycleCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateProjectLifecycleCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateProjectLifecycleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var lifecycle = await _projectPortfolioManagementDbContext.ProjectLifecycles
                .Include(x => x.Phases)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (lifecycle is null)
            {
                _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} not found.", request.Id);
                return Result.Failure("Project Lifecycle not found.");
            }

            var activateResult = lifecycle.Activate();
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(lifecycle).ReloadAsync(cancellationToken);
                lifecycle.ClearDomainEvents();

                _logger.LogError("Unable to activate Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.Id, activateResult.Error);

                return Result.Failure(activateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Project Lifecycle {ProjectLifecycleId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
