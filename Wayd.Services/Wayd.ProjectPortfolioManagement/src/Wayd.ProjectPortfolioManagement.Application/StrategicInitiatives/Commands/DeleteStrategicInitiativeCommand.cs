namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record DeleteStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class DeleteStrategicInitiativeCommandValidator : AbstractValidator<DeleteStrategicInitiativeCommand>
{
    public DeleteStrategicInitiativeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteStrategicInitiativeCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<DeleteStrategicInitiativeCommandHandler> logger)
    : ICommandHandler<DeleteStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(DeleteStrategicInitiativeCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteStrategicInitiativeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicInitiative = await _projectPortfolioManagementDbContext.StrategicInitiatives
                .Include(s => s.Roles)
                .Include(s => s.Kpis)
                    .ThenInclude(k => k.Measurements)
                .Include(s => s.Kpis)
                    .ThenInclude(k => k.Checkpoints)
                .Include(s => s.StrategicInitiativeProjects)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (strategicInitiative == null)
            {
                _logger.LogInformation("Strategic Initiative with Id {StrategicInitiativeId} not found.", request.Id);
                return Result.Failure("Strategic Initiative not found.");
            }

            var portfolio = await _projectPortfolioManagementDbContext.Portfolios
                    .Include(p => p.StrategicInitiatives.Where(i => i.Id == request.Id))
                    // The rest of the strategic initiative relationships are already include from the initial strategicInitiative query
                    .FirstOrDefaultAsync(p => p.Id == strategicInitiative.PortfolioId, cancellationToken);
            if (portfolio == null)
            {
                _logger.LogInformation("Portfolio with Id {PortfolioId} not found.", strategicInitiative.PortfolioId);
                return Result.Failure("Portfolio not found.");
            }

            var deleteResult = portfolio.DeleteStrategicInitiative(strategicInitiative.Id);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Error deleting Strategic Initiative {StrategicInitiativeId}. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} with Key {StrategicInitiativeKey} deleted", strategicInitiative.Id, strategicInitiative.Key);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
