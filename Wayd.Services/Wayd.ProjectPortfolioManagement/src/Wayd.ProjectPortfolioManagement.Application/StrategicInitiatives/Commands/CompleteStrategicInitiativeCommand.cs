namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record CompleteStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class CompleteStrategicInitiativeCommandValidator : AbstractValidator<CompleteStrategicInitiativeCommand>
{
    public CompleteStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class CompleteStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<CompleteStrategicInitiativeCommandHandler> logger) : ICommandHandler<CompleteStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(CompleteStrategicInitiativeCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CompleteStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompleteStrategicInitiativeCommand request, CancellationToken cancellationToken)
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

            var completeResult = strategicInitiative.Complete();
            if (completeResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);

                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Unable to complete Strategic Initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, completeResult.Error);

                return Result.Failure(completeResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} completed.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
