using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;

public sealed record GetProjectScoresQuery(Guid ProjectId)
    : IQuery<IReadOnlyList<ProjectScoreSummaryDto>>;

public sealed class GetProjectScoresQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectScoresQuery, IReadOnlyList<ProjectScoreSummaryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectScoreSummaryDto>> Handle(GetProjectScoresQuery request, CancellationToken cancellationToken)
    {
        var scores = await _ppmDbContext.ProjectScores
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId)
            .OrderByDescending(s => s.Sequence)
            .ProjectToType<ProjectScoreSummaryDto>()
            .ToListAsync(cancellationToken);

        return scores;
    }
}
