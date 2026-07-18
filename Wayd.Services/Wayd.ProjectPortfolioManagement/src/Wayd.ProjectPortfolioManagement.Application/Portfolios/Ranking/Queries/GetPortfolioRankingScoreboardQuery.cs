using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;

/// <summary>
/// Loads the per-project score breakdown for a portfolio's ranking board: the portfolio's current
/// scoring model definition (for column headers) and, per project, the criterion ratings and output
/// values from its current score — only when that score was produced by the current model. Returns
/// null when the portfolio does not exist.
/// </summary>
public sealed record GetPortfolioRankingScoreboardQuery(Guid PortfolioId)
    : IQuery<PortfolioRankingScoreboardDto?>;

public sealed class GetPortfolioRankingScoreboardQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetPortfolioRankingScoreboardQuery, PortfolioRankingScoreboardDto?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<PortfolioRankingScoreboardDto?> Handle(GetPortfolioRankingScoreboardQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await _ppmDbContext.Portfolios
            .Where(p => p.Id == request.PortfolioId)
            .Select(p => new { p.ScoringModelId })
            .FirstOrDefaultAsync(cancellationToken);

        if (portfolio is null)
            return null;

        var result = new PortfolioRankingScoreboardDto();

        // No assigned model → no breakdown columns; the board still renders rank/name/status.
        if (portfolio.ScoringModelId is not { } modelId)
            return result;

        result.ScoringModel = await _ppmDbContext.ScoringModels
            .AsSplitQuery()
            .Where(m => m.Id == modelId)
            .ProjectToType<ScoringModelDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);

        // The portfolio's project ids (ProjectScore has no Project navigation, so filter scores by id).
        var projectIds = await _ppmDbContext.Projects
            .Where(p => p.PortfolioId == request.PortfolioId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // Step 1 (scalar group-by): find each project's CURRENT score id (highest Sequence). EF can
        // translate group-by + First() when only scalars are projected — not when child collections
        // are pulled from the grouped element, so we resolve the id here, then load the full snapshot.
        var currentScoreIds = await _ppmDbContext.ProjectScores
            .Where(s => projectIds.Contains(s.ProjectId))
            .GroupBy(s => s.ProjectId)
            .Select(g => g.OrderByDescending(s => s.Sequence).First().Id)
            .ToListAsync(cancellationToken);

        // Step 2: load those current scores with ratings/outputs, keeping only the ones produced by the
        // current model. A project whose current score used a different/older model is excluded — the
        // breakdown reflects the project's *current* score, never a stale one, so it can't show under
        // mismatched headers.
        result.Projects = await _ppmDbContext.ProjectScores
            .AsSplitQuery()
            .Where(s => currentScoreIds.Contains(s.Id) && s.ScoringModelId == modelId)
            .Select(s => new ProjectRankingScoreDto
            {
                ProjectId = s.ProjectId,
                Ratings = s.Ratings
                    .OrderBy(r => r.Order)
                    .Select(r => new ProjectScoreRatingDto
                    {
                        CriterionId = r.CriterionId,
                        CriterionName = r.CriterionName,
                        CriterionToken = r.CriterionToken,
                        RatingValue = r.RatingValue,
                        RatingLevelId = r.RatingLevelId,
                        RatingLevelLabel = r.RatingLevelLabel,
                        Order = r.Order,
                    })
                    .ToList(),
                Outputs = s.Outputs
                    .OrderBy(o => o.Order)
                    .Select(o => new ProjectScoreOutputDto
                    {
                        Token = o.Token,
                        Name = o.Name,
                        Value = o.Value,
                        IsPrimary = o.IsPrimary,
                        Order = o.Order,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return result;
    }
}
