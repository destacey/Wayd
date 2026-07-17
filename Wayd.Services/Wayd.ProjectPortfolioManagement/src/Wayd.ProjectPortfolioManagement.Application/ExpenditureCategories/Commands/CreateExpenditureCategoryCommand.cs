using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;

public sealed record CreateExpenditureCategoryCommand(
    string Name,
    string Description,
    bool IsCapitalizable,
    bool RequiresDepreciation,
    string? AccountingCode)
    : ICommand<int>;

public sealed class CreateExpenditureCategoryCommandValidator : AbstractValidator<CreateExpenditureCategoryCommand>
{
    public CreateExpenditureCategoryCommandValidator()
    {
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

public sealed class CreateExpenditureCategoryCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<CreateExpenditureCategoryCommandHandler> logger)
    : ICommandHandler<CreateExpenditureCategoryCommand, int>
{
    private const string AppRequestName = nameof(CreateExpenditureCategoryCommand);

    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<CreateExpenditureCategoryCommandHandler> _logger = logger;

    public async Task<Result<int>> Handle(CreateExpenditureCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var expenditureCategory = ExpenditureCategory.Create(
                request.Name,
                request.Description,
                request.IsCapitalizable,
                request.RequiresDepreciation,
                request.AccountingCode
                );

            await _projectPortfolioManagementDbContext.ExpenditureCategories.AddAsync(expenditureCategory, cancellationToken);
            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} created.", expenditureCategory.Id);

            return Result.Success(expenditureCategory.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<int>($"Error handling {AppRequestName} command.");
        }
    }
}
