using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Queries;

/// <summary>
/// Retrieves every project portfolio as an option.
/// </summary>
public sealed record GetProjectPortfolioOptionsQuery() : IQuery<List<ProjectPortfolioOptionDto>>;

public sealed class GetProjectPortfolioOptionsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectPortfolioOptionsQuery, List<ProjectPortfolioOptionDto>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ProjectPortfolioOptionDto>> Handle(GetProjectPortfolioOptionsQuery request, CancellationToken cancellationToken)
    {
        var portfolios = await _projectPortfolioManagementDbContext.Portfolios
            .Where(p => p.Status == ProjectPortfolioStatus.Active)
            .ProjectToType<ProjectPortfolioOptionDto>()
            .ToListAsync(cancellationToken);

        return [.. portfolios.OrderBy(p => p.Name).ThenBy(p => p.Id)];
    }
}
