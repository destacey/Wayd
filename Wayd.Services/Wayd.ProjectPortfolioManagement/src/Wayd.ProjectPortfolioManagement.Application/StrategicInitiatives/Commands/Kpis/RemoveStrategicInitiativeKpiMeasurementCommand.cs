namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record RemoveStrategicInitiativeKpiMeasurementCommand(Guid StrategicInitiativeId, Guid KpiId, Guid MeasurementId) : ICommand;

public sealed class RemoveStrategicInitiativeKpiMeasurementCommandValidator : AbstractValidator<RemoveStrategicInitiativeKpiMeasurementCommand>
{
    public RemoveStrategicInitiativeKpiMeasurementCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();

        RuleFor(x => x.MeasurementId)
            .NotEmpty();
    }
}

public sealed class RemoveStrategicInitiativeKpiMeasurementCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<RemoveStrategicInitiativeKpiMeasurementCommandHandler> logger)
    : ICommandHandler<RemoveStrategicInitiativeKpiMeasurementCommand>
{
    private const string AppRequestName = nameof(RemoveStrategicInitiativeKpiMeasurementCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<RemoveStrategicInitiativeKpiMeasurementCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveStrategicInitiativeKpiMeasurementCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(i => i.Kpis.Where(k => k.Id == request.KpiId))
                    .ThenInclude(k => k.Measurements)
                .FirstOrDefaultAsync(i => i.Id == request.StrategicInitiativeId, cancellationToken);

            if (strategicInitiative is null)
            {
                _logger.LogInformation(
                    "Strategic Initiative with Id {StrategicInitiativeId} not found.",
                    request.StrategicInitiativeId);

                return Result.Failure("Strategic Initiative not found.");
            }

            var kpi = strategicInitiative.Kpis.FirstOrDefault(k => k.Id == request.KpiId);
            if (kpi is null)
            {
                _logger.LogInformation(
                    "KPI with Id {KpiId} not found for Strategic Initiative {StrategicInitiativeId}.",
                    request.KpiId,
                    request.StrategicInitiativeId);

                return Result.Failure("KPI not found.");
            }

            var removeResult = kpi.RemoveMeasurement(request.MeasurementId);
            if (removeResult.IsFailure)
            {
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);
                strategicInitiative.ClearDomainEvents();

                _logger.LogError(
                    "Error removing KPI measurement {MeasurementId} for Strategic Initiative {StrategicInitiativeId}. Error message: {Error}",
                    request.MeasurementId,
                    request.StrategicInitiativeId,
                    removeResult.Error);

                return Result.Failure(removeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "KPI measurement {MeasurementId} removed for Strategic Initiative {StrategicInitiativeId}.",
                request.MeasurementId,
                request.StrategicInitiativeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
