using Wayd.Common.Application.Scoring.ScoringModels.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// The data the record-score form needs when it opens: the assigned model's definition (for rendering
/// the rating inputs), whether that model is archived (to warn), and the project's current score (to
/// seed a re-score). Display and authorization for the score card live on the project DTO; this context
/// is fetched lazily only when the form is launched.
/// </summary>
public sealed record ProjectScoringContextDto
{
    /// <summary>
    /// The assigned scoring model's definition (criteria, scales, outputs). Null when the portfolio has
    /// no model assigned.
    /// </summary>
    public ScoringModelDetailsDto? ScoringModel { get; set; }

    /// <summary>
    /// True when the assigned model is no longer active (archived). Scoring still works, but the form
    /// should warn and nudge an admin to assign a newer model.
    /// </summary>
    public bool ScoringModelArchived { get; set; }

    /// <summary>
    /// The project's current score, or null if it has never been scored. Used to seed a re-score.
    /// </summary>
    public ProjectScoreDetailsDto? CurrentScore { get; set; }
}
