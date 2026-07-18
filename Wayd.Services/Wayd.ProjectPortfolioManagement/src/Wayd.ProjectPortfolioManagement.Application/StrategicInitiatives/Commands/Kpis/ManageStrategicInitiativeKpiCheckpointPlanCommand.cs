using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record StrategicInitiativeKpiCheckpointPlanItem(
    Guid? CheckpointId,
    double TargetValue,
    double? AtRiskValue,
    Instant CheckpointDate,
    string DateLabel);

public sealed record ManageStrategicInitiativeKpiCheckpointPlanCommand(
    Guid StrategicInitiativeId,
    Guid KpiId,
    IReadOnlyCollection<StrategicInitiativeKpiCheckpointPlanItem> Checkpoints) : ICommand;

public sealed class ManageStrategicInitiativeKpiCheckpointPlanCommandValidator : AbstractValidator<ManageStrategicInitiativeKpiCheckpointPlanCommand>
{
    private readonly IProjectPortfolioManagementDbContext _dbContext;

    public ManageStrategicInitiativeKpiCheckpointPlanCommandValidator(IProjectPortfolioManagementDbContext dbContext)
    {
        _dbContext = dbContext;

        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();

        RuleFor(x => x.Checkpoints)
            .NotNull()
            .Must(checkpoints => checkpoints
                .Where(i => i.CheckpointId.HasValue)
                .Select(i => i.CheckpointId!.Value)
                .Distinct()
                .Count() == checkpoints.Count(i => i.CheckpointId.HasValue))
            .WithMessage("Checkpoint IDs must be unique.");

        RuleFor(x => x)
            .MustAsync(HaveValidAtRiskValues)
            .WithName("Checkpoints")
            .WithMessage("At-risk values are invalid for the KPI's target direction.");
    }

    private async Task<bool> HaveValidAtRiskValues(ManageStrategicInitiativeKpiCheckpointPlanCommand command, CancellationToken cancellationToken)
    {
        var itemsWithAtRiskValue = command.Checkpoints
            .Where(c => c.AtRiskValue.HasValue)
            .ToList();

        if (itemsWithAtRiskValue.Count == 0)
            return true;

        var targetDirection = await _dbContext.StrategicInitiatives
            .Where(i => i.Id == command.StrategicInitiativeId)
            .SelectMany(i => i.Kpis)
            .Where(k => k.Id == command.KpiId)
            .Select(k => (KpiTargetDirection?)k.TargetDirection)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetDirection is null)
            return true;

        return itemsWithAtRiskValue.All(c => IsValidAtRiskValue(c.AtRiskValue!.Value, c.TargetValue, targetDirection.Value));
    }

    private static bool IsValidAtRiskValue(double atRiskValue, double targetValue, KpiTargetDirection direction)
        => direction switch
        {
            KpiTargetDirection.Increase => atRiskValue < targetValue,
            KpiTargetDirection.Decrease => atRiskValue > targetValue,
            _ => true
        };
}

public sealed class StrategicInitiativeKpiCheckpointPlanItemValidator : AbstractValidator<StrategicInitiativeKpiCheckpointPlanItem>
{
    public StrategicInitiativeKpiCheckpointPlanItemValidator()
    {
        RuleFor(x => x.CheckpointId)
            .NotEmpty()
            .When(x => x.CheckpointId.HasValue);

        RuleFor(x => x.AtRiskValue)
            .NotEqual(x => x.TargetValue)
            .When(x => x.AtRiskValue.HasValue)
            .WithMessage("At-risk value must differ from target value.");

        RuleFor(x => x.CheckpointDate)
            .NotEmpty();

        RuleFor(x => x.DateLabel)
            .NotEmpty()
            .MaximumLength(16);
    }
}

public sealed class ManageStrategicInitiativeKpiCheckpointPlanCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ManageStrategicInitiativeKpiCheckpointPlanCommandHandler> logger)
    : ICommandHandler<ManageStrategicInitiativeKpiCheckpointPlanCommand>
{
    private const string AppRequestName = nameof(ManageStrategicInitiativeKpiCheckpointPlanCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ManageStrategicInitiativeKpiCheckpointPlanCommandHandler> _logger = logger;

    public async Task<Result> Handle(ManageStrategicInitiativeKpiCheckpointPlanCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(i => i.Kpis.Where(k => k.Id == request.KpiId))
                    .ThenInclude(k => k.Checkpoints)
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

            var checkpoints = request.Checkpoints
                .Select(c => UpsertStrategicInitiativeKpiCheckpoint.Create(c.CheckpointId, c.TargetValue, c.CheckpointDate, c.DateLabel, c.AtRiskValue))
                .ToList();

            var manageResult = kpi.ManageCheckpointPlan(checkpoints);
            if (manageResult.IsFailure)
            {
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);
                strategicInitiative.ClearDomainEvents();

                _logger.LogError(
                    "Error managing KPI checkpoint plan for Strategic Initiative {StrategicInitiativeId}. Error message: {Error}",
                    request.StrategicInitiativeId,
                    manageResult.Error);

                return Result.Failure(manageResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "KPI checkpoint plan updated for Strategic Initiative {StrategicInitiativeId}.",
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
