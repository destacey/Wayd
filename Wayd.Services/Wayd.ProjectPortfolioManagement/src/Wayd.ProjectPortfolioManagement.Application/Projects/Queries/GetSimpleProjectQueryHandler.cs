using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed class GetSimpleProjectQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetSimpleProjectQuery, ISimpleProject?>
{
    private readonly IProjectPortfolioManagementDbContext _projectPortfolioManagementDbContext = projectPortfolioManagementDbContext;

    public async Task<ISimpleProject?> Handle(GetSimpleProjectQuery request, CancellationToken cancellationToken)
    {
        return await _projectPortfolioManagementDbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
    }
}
