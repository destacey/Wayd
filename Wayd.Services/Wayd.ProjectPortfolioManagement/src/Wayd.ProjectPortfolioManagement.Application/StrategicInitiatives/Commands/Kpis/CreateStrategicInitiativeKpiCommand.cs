using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Validators;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;

public sealed record CreateStrategicInitiativeKpiCommand(Guid StrategicInitiativeId, StrategicInitiativeKpiUpsertParameters UpsertParameters) : ICommand<ObjectIdAndKey>;

public sealed class CreateStrategicInitiativeKpiCommandValidator : AbstractValidator<CreateStrategicInitiativeKpiCommand>
{
    public CreateStrategicInitiativeKpiCommandValidator()
    {
        RuleFor(x => x.StrategicInitiativeId)
            .NotEmpty();

        RuleFor(x => x.UpsertParameters)
            .NotNull()
            .SetValidator(new StrategicInitiativeKpiUpsertParametersValidator());
    }
}

public sealed class CreateStrategicInitiativeKpiCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateStrategicInitiativeKpiCommandHandler> logger)
    : ICommandHandler<CreateStrategicInitiativeKpiCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateStrategicInitiativeKpiCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateStrategicInitiativeKpiCommandHandler> _logger = logger;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateStrategicInitiativeKpiCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                    .FirstOrDefaultAsync(si => si.Id == request.StrategicInitiativeId, cancellationToken);
            if (strategicInitiative == null)
            {
                _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.StrategicInitiativeId);
                return Result.Failure<ObjectIdAndKey>("Strategic Initiative not found.");
            }

            var createResult = strategicInitiative.CreateKpi(request.UpsertParameters);
            if (createResult.IsFailure)
            {
                _logger.LogError("Error creating KPI {StrategicInitiativeKpiName} for strategic initiative {StrategicInitiativeId}. Error message: {Error}", request.UpsertParameters.Name, request.StrategicInitiativeId, createResult.Error);
                return Result.Failure<ObjectIdAndKey>(createResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            var kpi = createResult.Value;

            _logger.LogInformation("KPI {StrategicInitiativeKpiId} created with Key {StrategicInitiativeKpiKey} for strategic initiative {StrategicInitiativeId}.", kpi.Id, kpi.Key, request.StrategicInitiativeId);

            return Result.Success(new ObjectIdAndKey(kpi.Id, kpi.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
