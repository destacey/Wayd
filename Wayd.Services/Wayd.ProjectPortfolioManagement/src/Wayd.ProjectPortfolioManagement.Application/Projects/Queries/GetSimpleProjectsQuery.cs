using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetSimpleProjectsQuery() : IQuery<List<ISimpleProject>>;

public sealed class GetSimpleProjectsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectsQuery, List<ISimpleProject>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ISimpleProject>> Handle(GetSimpleProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _projectPortfolioManagementDbContext.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. projects.OfType<ISimpleProject>()];
    }
}
