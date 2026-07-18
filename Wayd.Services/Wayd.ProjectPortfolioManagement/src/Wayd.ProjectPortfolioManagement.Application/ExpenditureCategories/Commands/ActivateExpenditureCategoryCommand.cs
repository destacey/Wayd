namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;

public sealed record ActivateExpenditureCategoryCommand(int Id) : ICommand;

public sealed class ActivateExpenditureCategoryCommandValidator : AbstractValidator<ActivateExpenditureCategoryCommand>
{
    public ActivateExpenditureCategoryCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);
    }
}

public sealed class ActivateExpenditureCategoryCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ActivateExpenditureCategoryCommandHandler> logger)
    : ICommandHandler<ActivateExpenditureCategoryCommand>
{
    private const string AppRequestName = nameof(ActivateExpenditureCategoryCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ActivateExpenditureCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateExpenditureCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var expenditureCategory = await _projectPortfolioManagementDbContext.ExpenditureCategories
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (expenditureCategory is null)
            {
                _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} not found.", request.Id);
                return Result.Failure("Expenditure Category not found.");
            }

            var activateResult = expenditureCategory.Activate();
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(expenditureCategory).ReloadAsync(cancellationToken);
                expenditureCategory.ClearDomainEvents();

                _logger.LogError("Unable to activate Expenditure Category {ExpenditureCategoryId}.  Error message: {Error}", request.Id, activateResult.Error);

                return Result.Failure(activateResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
