using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Queries;

public sealed record GetProjectPortfolioQuery : IQuery<ProjectPortfolioDetailsDto?>
{
    public GetProjectPortfolioQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ProjectPortfolio>();
    }

    public Expression<Func<ProjectPortfolio, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProjectPortfolioQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectPortfolioQuery, ProjectPortfolioDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<ProjectPortfolioDetailsDto?> Handle(GetProjectPortfolioQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.Portfolios
            .Where(request.IdOrKeyFilter)
            .ProjectToType<ProjectPortfolioDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
