using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Queries;

public sealed record GetProjectLifecyclesQuery(ProjectLifecycleState? StateFilter = null) : IQuery<List<ProjectLifecycleListDto>>;

internal sealed class GetProjectLifecyclesQueryHandler(IProjectPortfolioManagementDbContext projectPortfolioManagementDbContext)
    : IQueryHandler<GetProjectLifecyclesQuery, List<ProjectLifecycleListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = projectPortfolioManagementDbContext;

    public async Task<List<ProjectLifecycleListDto>> Handle(GetProjectLifecyclesQuery request, CancellationToken cancellationToken)
    {
        var query = _ppmDbContext.ProjectLifecycles.AsQueryable();

        if (request.StateFilter.HasValue)
        {
            query = query.Where(x => x.State == request.StateFilter.Value);
        }

        return await query
            .ProjectToType<ProjectLifecycleListDto>()
            .ToListAsync(cancellationToken);
    }
}
