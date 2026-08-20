using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStagesQuery(Guid ProjectId) : IQuery<List<ProjectStageListDto>>;

public sealed class GetProjectStagesQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectStagesQuery, List<ProjectStageListDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<List<ProjectStageListDto>> Handle(GetProjectStagesQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.ProjectStages
            .Where(p => p.ProjectId == request.ProjectId)
            .OrderBy(p => p.Order)
            .ProjectToType<ProjectStageListDto>()
            .ToListAsync(cancellationToken);
    }
}
