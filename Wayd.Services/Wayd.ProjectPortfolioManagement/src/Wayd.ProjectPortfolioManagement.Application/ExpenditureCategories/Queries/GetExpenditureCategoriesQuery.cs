using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;

public sealed record GetExpenditureCategoriesQuery() : IQuery<List<ExpenditureCategoryListDto>>;

public sealed class GetExpenditureCategoriesQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetExpenditureCategoriesQuery, List<ExpenditureCategoryListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ExpenditureCategoryListDto>> Handle(GetExpenditureCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.ExpenditureCategories
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ProjectToType<ExpenditureCategoryListDto>()
            .ToListAsync(cancellationToken);
    }
}

