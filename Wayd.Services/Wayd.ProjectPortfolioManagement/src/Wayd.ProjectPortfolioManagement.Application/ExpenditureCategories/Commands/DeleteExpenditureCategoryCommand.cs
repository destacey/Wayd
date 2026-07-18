using Wayd.Common.Application.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;

public sealed record DeleteExpenditureCategoryCommand(int Id) : ICommand;

public sealed class DeleteExpenditureCategoryCommandValidator : AbstractValidator<DeleteExpenditureCategoryCommand>
{
    public DeleteExpenditureCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}

public sealed class DeleteExpenditureCategoryCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<DeleteExpenditureCategoryCommandHandler> logger)
    : ICommandHandler<DeleteExpenditureCategoryCommand>
{
    private const string AppRequestName = nameof(DeleteExpenditureCategoryCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<DeleteExpenditureCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteExpenditureCategoryCommand request, CancellationToken cancellationToken)
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

            if (!expenditureCategory.CanBeDeleted())
            {
                _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} cannot be deleted.", request.Id);
                return Result.Failure("Expenditure Category cannot be deleted.");
            }

            _projectPortfolioManagementDbContext.ExpenditureCategories.Remove(expenditureCategory);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
