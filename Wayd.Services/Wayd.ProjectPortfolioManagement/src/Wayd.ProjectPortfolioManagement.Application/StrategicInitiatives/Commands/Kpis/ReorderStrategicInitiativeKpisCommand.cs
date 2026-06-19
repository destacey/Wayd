namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record ReorderStrategicInitiativeKpisCommand(
    Guid StrategicInitiativeId,
    List<Guid> OrderedKpiIds)
    : ICommand;

public sealed class ReorderStrategicInitiativeKpisCommandValidator : AbstractValidator<ReorderStrategicInitiativeKpisCommand>
{
    public ReorderStrategicInitiativeKpisCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.OrderedKpiIds)
            .NotEmpty();
    }
}

internal sealed class ReorderStrategicInitiativeKpisCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ReorderStrategicInitiativeKpisCommandHandler> logger)
    : ICommandHandler<ReorderStrategicInitiativeKpisCommand>
{
    private const string AppRequestName = nameof(ReorderStrategicInitiativeKpisCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ReorderStrategicInitiativeKpisCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderStrategicInitiativeKpisCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(i => i.Kpis)
                .FirstOrDefaultAsync(i => i.Id == request.StrategicInitiativeId, cancellationToken);
            if (strategicInitiative is null)
            {
                _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} not found.", request.StrategicInitiativeId);
                return Result.Failure("Strategic Initiative not found.");
            }

            var reorderResult = strategicInitiative.ReorderKpis(request.OrderedKpiIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder KPIs on Strategic Initiative {StrategicInitiativeId}.  Error message: {Error}", request.StrategicInitiativeId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("KPIs reordered on Strategic Initiative {StrategicInitiativeId}.", request.StrategicInitiativeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
