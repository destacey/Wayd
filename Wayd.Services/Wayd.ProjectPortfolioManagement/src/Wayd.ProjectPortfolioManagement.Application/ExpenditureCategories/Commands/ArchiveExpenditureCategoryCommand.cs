namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Commands;

public sealed record ArchiveExpenditureCategoryCommand(int Id) : ICommand;

public sealed class ArchiveExpenditureCategoryCommandValidator : AbstractValidator<ArchiveExpenditureCategoryCommand>
{
    public ArchiveExpenditureCategoryCommandValidator()
    {
        RuleFor(v => v.Id)
            .GreaterThan(0);
    }
}

public sealed class ArchiveExpenditureCategoryCommandHandler(
    IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext,
    ILogger<ArchiveExpenditureCategoryCommandHandler> logger)
    : ICommandHandler<ArchiveExpenditureCategoryCommand>
{
    private const string AppRequestName = nameof(ArchiveExpenditureCategoryCommand);
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;
    private readonly ILogger<ArchiveExpenditureCategoryCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveExpenditureCategoryCommand request, CancellationToken cancellationToken)
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

            var archiveResult = expenditureCategory.Archive();
            if (archiveResult.IsFailure)
            {
                // Reset the entity
                await _projectPortfolioManagementDbContext.Entry(expenditureCategory).ReloadAsync(cancellationToken);
                expenditureCategory.ClearDomainEvents();

                _logger.LogError("Unable to archive Expenditure Category {ExpenditureCategoryId}.  Error message: {Error}", request.Id, archiveResult.Error);

                return Result.Failure(archiveResult.Error);
            }

            await _projectPortfolioManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Expenditure Category {ExpenditureCategoryId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
