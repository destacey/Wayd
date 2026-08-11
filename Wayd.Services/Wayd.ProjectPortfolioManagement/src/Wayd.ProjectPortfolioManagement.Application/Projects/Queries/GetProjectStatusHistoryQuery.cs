using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStatusHistoryQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectStatusHistoryDto>>;

public sealed class GetProjectStatusHistoryQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectStatusHistoryQuery, IReadOnlyList<ProjectStatusHistoryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectStatusHistoryDto>> Handle(GetProjectStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.ProjectStatusHistory
            .Where(h => h.ProjectId == request.ProjectId)
            .OrderByDescending(h => h.ChangedOn)
            .ProjectToType<ProjectStatusHistoryDto>()
            .ToListAsync(cancellationToken);
    }
}
