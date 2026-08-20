using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectStageQuery(Guid ProjectId, Guid StageId) : IQuery<ProjectStageDetailsDto?>;

public sealed class GetProjectStageQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectStageQuery, ProjectStageDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<ProjectStageDetailsDto?> Handle(GetProjectStageQuery request, CancellationToken cancellationToken)
    {
        return await _ppmDbContext.ProjectStages
            .Where(p => p.ProjectId == request.ProjectId && p.Id == request.StageId)
            .ProjectToType<ProjectStageDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
