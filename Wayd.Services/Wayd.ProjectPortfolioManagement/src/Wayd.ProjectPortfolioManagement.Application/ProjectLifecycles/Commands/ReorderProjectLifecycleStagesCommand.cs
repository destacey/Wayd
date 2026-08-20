namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

public sealed record ReorderProjectLifecycleStagesCommand(
    Guid LifecycleId,
    List<Guid> OrderedStageIds)
    : ICommand;

public sealed class ReorderProjectLifecycleStagesCommandValidator : AbstractValidator<ReorderProjectLifecycleStagesCommand>
{
    public ReorderProjectLifecycleStagesCommandValidator()
    {
        RuleFor(x => x.LifecycleId)
            .NotEmpty();

        RuleFor(x => x.OrderedStageIds)
            .NotEmpty();
    }
}

public sealed class ReorderProjectLifecycleStagesCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ReorderProjectLifecycleStagesCommandHandler> logger)
    : ICommandHandler<ReorderProjectLifecycleStagesCommand>
{
    private const string AppRequestName = nameof(ReorderProjectLifecycleStagesCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ReorderProjectLifecycleStagesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderProjectLifecycleStagesCommand request, CancellationToken cancellationToken)
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

            var reorderResult = lifecycle.ReorderStages(request.OrderedStageIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder stages on Project Lifecycle {ProjectLifecycleId}.  Error message: {Error}", request.LifecycleId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Stages reordered on Project Lifecycle {ProjectLifecycleId}.", request.LifecycleId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
