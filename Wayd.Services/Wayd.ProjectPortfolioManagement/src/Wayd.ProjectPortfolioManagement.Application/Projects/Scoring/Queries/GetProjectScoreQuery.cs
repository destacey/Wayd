using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;

public sealed record GetProjectScoreQuery(Guid ProjectId, Guid ScoreId)
    : IQuery<ProjectScoreDetailsDto?>;

internal sealed class GetProjectScoreQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectScoreQuery, ProjectScoreDetailsDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<ProjectScoreDetailsDto?> Handle(GetProjectScoreQuery request, CancellationToken cancellationToken)
    {
        var score = await _ppmDbContext.ProjectScores
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId && s.Id == request.ScoreId)
            .ProjectToType<ProjectScoreDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return score;
    }
}
