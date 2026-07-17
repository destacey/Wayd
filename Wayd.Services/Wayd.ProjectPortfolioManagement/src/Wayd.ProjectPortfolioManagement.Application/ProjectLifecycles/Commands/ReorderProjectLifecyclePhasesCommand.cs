namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record ReorderProjectLifecyclePhasesCommand(
    Guid LifecycleId,
    List<Guid> OrderedPhaseIds)
    : ICommand;

public sealed class ReorderProjectLifecyclePhasesCommandValidator : AbstractValidator<ReorderProjectLifecyclePhasesCommand>
{
    public ReorderProjectLifecyclePhasesCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.OrderedPhaseIds)
            .NotEmpty();
    }
}

public sealed class ReorderProjectLifecyclePhasesCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ReorderProjectLifecyclePhasesCommandHandler> logger)
    : ICommandHandler<ReorderProjectLifecyclePhasesCommand>
{
    private const string AppRequestName = nameof(ReorderProjectLifecyclePhasesCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ReorderProjectLifecyclePhasesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderProjectLifecyclePhasesCommand request, CancellationToken cancellationToken)
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

            var reorderResult = lifecycle.ReorderPhases(request.OrderedPhaseIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder phases on Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.LifecycleId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Phases reordered on Project Lifecycle {ProjectLifecycleId}.", request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
