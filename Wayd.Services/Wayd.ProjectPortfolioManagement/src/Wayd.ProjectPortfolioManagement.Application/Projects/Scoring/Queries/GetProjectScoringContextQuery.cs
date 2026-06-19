using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Queries;

public sealed record GetProjectScoringContextQuery(Guid ProjectId)
    : IQuery<ProjectScoringContextDto?>;

internal sealed class GetProjectScoringContextQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectScoringContextQuery, ProjectScoringContextDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<ProjectScoringContextDto?> Handle(GetProjectScoringContextQuery request, CancellationToken cancellationToken)
    {
        // Project a nullable wrapper so a missing project (null) is distinguishable from a project whose
        // portfolio has no model assigned (present, ScoringModelId == null). The former returns 404.
        var project = await _ppmDbContext.Projects
            .AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .Select(p => new { p.Portfolio!.ScoringModelId })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return null;

        var scoringModelId = project.ScoringModelId;
        var context = new ProjectScoringContextDto();

        if (scoringModelId is not null)
        {
            var model = await _ppmDbContext.ScoringModels
                .AsNoTracking()
                .AsSplitQuery()
                .Include(m => m.Criteria)
                .Include(m => m.Scales).ThenInclude(s => s.Levels)
                .Include(m => m.Outputs)
                .FirstOrDefaultAsync(m => m.Id == scoringModelId.Value, cancellationToken);

            if (model is not null)
            {
                context.ScoringModel = model.Adapt<ScoringModelDetailsDto>();
                context.ScoringModelArchived = model.State == ScoringModelState.Archived;
            }
        }

        context.CurrentScore = await _ppmDbContext.ProjectScores
            .AsNoTracking()
            .Where(s => s.ProjectId == request.ProjectId)
            .OrderByDescending(s => s.Sequence)
            .ProjectToType<ProjectScoreDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return context;
    }
}
