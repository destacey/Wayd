using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>Every PPM project, for the job that brings each module's copies in line with them.</summary>
public sealed record GetSimpleProjectsQuery() : IQuery<List<ISimpleProjectWithLifecycle>>;

public sealed class GetSimpleProjectsQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectsQuery, List<ISimpleProjectWithLifecycle>>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ISimpleProjectWithLifecycle>> Handle(GetSimpleProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _projectPortfolioManagementDbContext.Projects
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. projects.OfType<ISimpleProjectWithLifecycle>()];
    }
}
