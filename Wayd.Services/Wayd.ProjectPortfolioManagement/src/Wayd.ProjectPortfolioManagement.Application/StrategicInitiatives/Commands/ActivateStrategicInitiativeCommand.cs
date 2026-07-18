namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record ActivateStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class ActivateStrategicInitiativeCommandValidator : AbstractValidator<ActivateStrategicInitiativeCommand>
{
    public ActivateStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ActivateStrategicInitiativeCommandHandler> logger) : ICommandHandler<ActivateStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(ActivateStrategicInitiativeCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (strategicInitiative is null)
            {
                _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} not found.", request.Id);
                return Result.Failure("Strategic Initiative not found.");
            }

            var activateResult = strategicInitiative.Activate();
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);

                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Unable to activate Strategic Initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, activateResult.Error);

                return Result.Failure(activateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
