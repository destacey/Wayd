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
            .ProjectToType<ExpenditureCategoryListDto>()
            .ToListAsync(cancellationToken);
    }
}

