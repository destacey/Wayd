using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Queries;

/// <summary>
/// Retrieves a list of ProjectPortfolios based on the provided filter.  Returns all ProjectPortfolios if no filter is provided.
/// </summary>
/// <param name="StatusFilter"></param>
public sealed record GetProjectPortfoliosQuery(ProjectPortfolioStatus[]? StatusFilter = null) : IQuery<List<ProjectPortfolioListDto>>;

public sealed class GetProjectPortfoliosQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectPortfoliosQuery, List<ProjectPortfolioListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ProjectPortfolioListDto>> Handle(GetProjectPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var query = _projectPortfolioManagementDbContext.Portfolios.AsQueryable();

        if (request.StatusFilter is { Length: > 0 })
        {
            query = query.Where(pp => request.StatusFilter.Contains(pp.Status));
        }

        var portfolios = await query.ProjectToType<ProjectPortfolioListDto>().ToListAsync(cancellationToken);
        return [.. portfolios.OrderBy(p => p.Name)];
    }
}
