using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;

public sealed record GetExpenditureCategoryQuery(int id) : IQuery<ExpenditureCategoryDetailsDto?>;

public sealed class GetExpenditureCategoryQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetExpenditureCategoryQuery, ExpenditureCategoryDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<ExpenditureCategoryDetailsDto?> Handle(GetExpenditureCategoryQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.ExpenditureCategories
            .Where(x => x.Id == request.id)
            .ProjectToType<ExpenditureCategoryDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}

