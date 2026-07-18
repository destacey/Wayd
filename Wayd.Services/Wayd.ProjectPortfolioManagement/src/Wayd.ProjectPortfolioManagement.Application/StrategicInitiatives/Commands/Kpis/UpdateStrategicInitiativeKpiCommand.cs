using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Validators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record UpdateStrategicInitiativeKpiCommand(Guid StrategicInitiativeId, Guid KpiId, StrategicInitiativeKpiUpsertParameters UpsertParameters) : ICommand;

public sealed class UpdateStrategicInitiativeKpiCommandValidator : AbstractValidator<UpdateStrategicInitiativeKpiCommand>
{
    public UpdateStrategicInitiativeKpiCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.KpiId)
            .NotEmpty();

        RuleFor(x => x.UpsertParameters)
            .NotNull()
            .SetValidator(new StrategicInitiativeKpiUpsertParametersValidator());
    }
}

public sealed class UpdateStrategicInitiativeKpiCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateStrategicInitiativeKpiCommandHandler> logger)
    : ICommandHandler<UpdateStrategicInitiativeKpiCommand>
{
    private const string AppRequestName = nameof(UpdateStrategicInitiativeKpiCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateStrategicInitiativeKpiCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateStrategicInitiativeKpiCommand request, CancellationToken cancellationToken)
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

            var updateResult = strategicInitiative.UpdateKpi(request.KpiId, request.UpsertParameters);
            if (updateResult.IsFailure)
            {
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);
                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Error updating strategic initiative KPI {StrategicInitiativeKpiId}. Error message: {Error}", request.KpiId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative KPI {StrategicInitiativeKpiId} updated.", request.KpiId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}

