namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record DeleteStrategicInitiativeKpiCommand(Guid StrategicInitiativeId, Guid KpiId) : ICommand;

public sealed class DeleteStrategicInitiativeKpiCommandValidator : AbstractValidator<DeleteStrategicInitiativeKpiCommand>
{
    public DeleteStrategicInitiativeKpiCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();
    }
}

public sealed class DeleteStrategicInitiativeKpiCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<DeleteStrategicInitiativeKpiCommandHandler> logger)
    : ICommandHandler<DeleteStrategicInitiativeKpiCommand>
{
    private const string AppRequestName = nameof(DeleteStrategicInitiativeKpiCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteStrategicInitiativeKpiCommandHandler> _logger = logger;
    public async Task<Result> Handle(DeleteStrategicInitiativeKpiCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(i => i.Kpis.Where(k => k.Id == request.KpiId))
                    .ThenInclude(k => k.Measurements)
                .Include(i => i.Kpis.Where(k => k.Id == request.KpiId))
                    .ThenInclude(k => k.Checkpoints)
                .FirstOrDefaultAsync(i => i.Id == request.StrategicInitiativeId, cancellationToken);
            if (strategicInitiative == null)
            {
                _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.StrategicInitiativeId);
                return Result.Failure("Strategic Initiative not found.");
            }

            var deleteResult = strategicInitiative.DeleteKpi(request.KpiId);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Error deleting Strategic Initiative KPI {StrategicInitiativeKpiId}. Error message: {Error}", request.KpiId, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative KPI {StrategicInitiativeKpiId} deleted.", request.KpiId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
