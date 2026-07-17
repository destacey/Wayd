namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;

public sealed record ApproveStrategicInitiativeCommand(Guid Id) : ICommand;

public sealed class ApproveStrategicInitiativeCommandValidator : AbstractValidator<ApproveStrategicInitiativeCommand>
{
    public ApproveStrategicInitiativeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ApproveStrategicInitiativeCommandHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext, ILogger<ApproveStrategicInitiativeCommandHandler> logger) : ICommandHandler<ApproveStrategicInitiativeCommand>
{
    private const string AppRequestName = nameof(ApproveStrategicInitiativeCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ApproveStrategicInitiativeCommandHandler> _logger = logger;

    public async Task<Result> Handle(ApproveStrategicInitiativeCommand request, CancellationToken cancellationToken)
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

            var approveResult = strategicInitiative.Approve();
            if (approveResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(strategicInitiative).ReloadAsync(cancellationToken);

                strategicInitiative.ClearDomainEvents();

                _logger.LogError("Unable to approve strategic initiative {StrategicInitiativeId}.  Error message: {Error}", request.Id, approveResult.Error);

                return Result.Failure(approveResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Initiative {StrategicInitiativeId} approved.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
