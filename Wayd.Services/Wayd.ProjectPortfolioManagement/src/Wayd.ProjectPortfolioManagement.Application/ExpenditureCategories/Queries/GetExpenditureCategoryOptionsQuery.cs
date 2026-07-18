using Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.ExpenditureCategories.Queries;

public sealed record GetExpenditureCategoryOptionsQuery(bool? IncludeArchived) : IQuery<List<ExpenditureCategoryOptionDto>>;

public sealed class GetExpenditureCategoryOptionsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetExpenditureCategoryOptionsQuery, List<ExpenditureCategoryOptionDto>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ExpenditureCategoryOptionDto>> Handle(GetExpenditureCategoryOptionsQuery request, CancellationToken cancellationToken)
    {
        List<ExpenditureCategoryState> statusFilter = request.IncludeArchived ?? false
            ? [ExpenditureCategoryState.Active, ExpenditureCategoryState.Archived]
            : [ExpenditureCategoryState.Active];

        var categories = await _projectPortfolioManagementDbContext.ExpenditureCategories
            .Where(c => statusFilter.Contains(c.State))
            .ProjectToType<ExpenditureCategoryOptionDto>()
            .ToListAsync(cancellationToken);

        return [.. categories.OrderBy(c => c.Name)];
    }
}
