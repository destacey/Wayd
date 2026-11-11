using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>Reads one project for a module that copies it with its lifecycle category.</summary>
public sealed class GetSimpleProjectWithLifecycleQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectWithLifecycleQuery, ISimpleProjectWithLifecycle?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<ISimpleProjectWithLifecycle?> Handle(GetSimpleProjectWithLifecycleQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
    }
}
