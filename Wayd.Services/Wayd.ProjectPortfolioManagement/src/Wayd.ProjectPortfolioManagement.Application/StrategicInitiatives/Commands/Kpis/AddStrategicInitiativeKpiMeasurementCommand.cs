using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record AddStrategicInitiativeKpiMeasurementCommand(Guid StrategicInitiativeId, Guid KpiId, double ActualValue, Instant MeasurementDate, string? Note) : ICommand, IRequireLinkedEmployee;

public sealed class AddStrategicInitiativeKpiMeasurementCommandValidator : AbstractValidator<AddStrategicInitiativeKpiMeasurementCommand>
{
    public AddStrategicInitiativeKpiMeasurementCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();

        RuleFor(x => x.ActualValue)
            .NotEmpty();

        RuleFor(x => x.MeasurementDate)
            .NotEmpty();

        RuleFor(x => x.Note)
            .MaximumLength(1024);
    }
}

public sealed class AddStrategicInitiativeKpiMeasurementCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<AddStrategicInitiativeKpiMeasurementCommandHandler> logger,
    IDateTimeProvider dateTimeProvider,
    ICurrentPrincipal currentPrincipal)
    : ICommandHandler<AddStrategicInitiativeKpiMeasurementCommand>
{
    private const string AppRequestName = nameof(AddStrategicInitiativeKpiMeasurementCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<AddStrategicInitiativeKpiMeasurementCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<Result> Handle(AddStrategicInitiativeKpiMeasurementCommand request, CancellationToken cancellationToken)
    {
        // A measurement records who took it, so this needs a linked employee. Outside the try because
        // the catch-all below would turn the refusal into a generic failure.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

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

            var kpi = strategicInitiative.Kpis.FirstOrDefault(k => k.Id == request.KpiId);
            if (kpi == null)
            {
                _logger.LogInformation("KPI with Id {KpiId} not found for Strategic Initiative {StrategicInitiativeId}.", request.KpiId, request.StrategicInitiativeId);
                return Result.Failure("KPI not found.");
            }

            var measurementResult = StrategicInitiativeKpiMeasurement.Create(request.KpiId, request.ActualValue, request.MeasurementDate, employeeId.Value, request.Note, _dateTimeProvider.Now);
            if (measurementResult.IsFailure)
            {
                _logger.LogError("Error creating KPI measurement for Strategic Initiative {StrategicInitiativeId}. Error message: {Error}", request.StrategicInitiativeId, measurementResult.Error);
                return Result.Failure(measurementResult.Error);
            }

            var addResult = kpi.AddMeasurement(measurementResult.Value);
            if (addResult.IsFailure)
            {
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);
                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Error adding KPI measurement for Strategic Initiative {StrategicInitiativeId}. Error message: {Error}", request.StrategicInitiativeId, addResult.Error);

                return Result.Failure(addResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("KPI measurement added for Strategic Initiative {StrategicInitiativeId}.", request.StrategicInitiativeId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
