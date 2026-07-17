namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record CancelStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class CancelStrategicInitiativeCommandValidator : AbstractValidator<CancelStrategicInitiativeCommand>
{
    public CancelStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CancelStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CancelStrategicInitiativeCommandHandler> logger) : ICommandHandler<CancelStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(CancelStrategicInitiativeCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CancelStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result> Handle(CancelStrategicInitiativeCommand request, CancellationToken cancellationToken)
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

            var cancelResult = strategicInitiative.Cancel();
            if (cancelResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);

                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Unable to cancel Strategic Initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, cancelResult.Error);

                return Result.Failure(cancelResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} cancelled.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
