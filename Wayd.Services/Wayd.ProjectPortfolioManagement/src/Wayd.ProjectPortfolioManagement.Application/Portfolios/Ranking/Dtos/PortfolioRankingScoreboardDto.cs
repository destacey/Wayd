using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Dtos;

/// <summary>
/// Score breakdown for a portfolio's ranking board: the portfolio's current scoring model definition
/// plus, per project, the criterion ratings and output values from that project's current score — but
/// only when the current score was produced by the portfolio's current model. Projects scored under a
/// different or older model, or not yet scored, are left out.
/// </summary>
/// <remarks>
/// The client builds a column per criterion and per output from the model; omitted projects render
/// blank breakdown cells.
/// </remarks>
public sealed record PortfolioRankingScoreboardDto
{
    /// <summary>The portfolio's current scoring model definition, or null if no model is assigned.</summary>
    public ScoringModelDetailsDto? ScoringModel { get; set; }

    /// <summary>Per-project score breakdown for the current model; projects with no current-model score are omitted.</summary>
    public List<ProjectRankingScoreDto> Projects { get; set; } = [];
}

/// <summary>A single project's current-model score breakdown for the ranking board.</summary>
public sealed record ProjectRankingScoreDto
{
    public Guid ProjectId { get; set; }

    /// <summary>Criterion ratings from the current-model score; empty when there is no matching score.</summary>
    public List<ProjectScoreRatingDto> Ratings { get; set; } = [];

    /// <summary>Output values from the current-model score; empty when there is no matching score.</summary>
    public List<ProjectScoreOutputDto> Outputs { get; set; } = [];
}
