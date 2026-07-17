namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;

public sealed record UpdateExpenditureCategoryCommand(
    int Id,
    string Name,
    string Description,
    bool IsCapitalizable,
    bool RequiresDepreciation,
    string? AccountingCode)
    : ICommand;

public sealed class UpdateExpenditureCategoryCommandValidator : AbstractValidator<UpdateExpenditureCategoryCommand>
{
    public UpdateExpenditureCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleFor(x => x.AccountingCode)
            .MaximumLength(64);
    }
}

public sealed class UpdateExpenditureCategoryCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<UpdateExpenditureCategoryCommandHandler> logger)
    : ICommandHandler<UpdateExpenditureCategoryCommand>
{
    private const string AppRequestName = nameof(UpdateExpenditureCategoryCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<UpdateExpenditureCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateExpenditureCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var expenditureCategory = await _projectPortfolioManagementDbContext.ExpenditureCategories
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (expenditureCategory is null)
            {
                _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} not found.", request.Id);
                return Result.Failure("Expenditure Category not found.");
            }

            var updateResult = expenditureCategory.Update(
                request.Name,
                request.Description,
                request.IsCapitalizable,
                request.RequiresDepreciation,
                request.AccountingCode
                );
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update Expenditure Category {ExpenditureCategoryId}.  Error message: {Error}", expenditureCategory.Id, updateResult.Error);
                return Result.Failure("Expenditure Category update failed.");
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<int>($"Error handling {AppRequestName} command.");
        }
    }
}
