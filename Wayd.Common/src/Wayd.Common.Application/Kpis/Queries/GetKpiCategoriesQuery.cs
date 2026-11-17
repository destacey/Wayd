using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Kpis.Queries;

/// <summary>
/// The organization's KPI categories in order, each with how many KPIs have it. Anyone signed in may read them: a KPI
/// form offers them.
/// </summary>
/// <param name="IncludeInactive">Include categories no longer offered for new KPIs.</param>
public sealed record GetKpiCategoriesQuery(bool IncludeInactive) : IQuery<List<KpiCategoryDto>>;

/// <summary>Reads the categories, ordered by position then name.</summary>
public sealed class GetKpiCategoriesQueryHandler(IKpisDbContext dbContext) : IQueryHandler<GetKpiCategoriesQuery, List<KpiCategoryDto>>
{
    private readonly IKpisDbContext _dbContext = dbContext;

    public async Task<List<KpiCategoryDto>> Handle(GetKpiCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = _dbContext.KpiCategories.AsQueryable();
        if (!request.IncludeInactive)
            categories = categories.Where(c => c.IsActive);

        return await categories
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new KpiCategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Order,
                c.IsActive,
                _dbContext.Kpis.Count(k => k.CategoryId == c.Id)))
            .ToListAsync(cancellationToken);
    }
}
