using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// The denormalised current-score summary for a project, surfaced so the score, its
/// date, who set it, and the model can be shown without joining to the score history.
/// </summary>
public sealed record ScoreSummaryDto
{
    public decimal Value { get; set; }
    public Instant ScoredOn { get; set; }
    public Guid ScoringModelId { get; set; }

    /// <summary>
    /// The employee who recorded the score. May be null if the scorer is not found.
    /// </summary>
    public EmployeeNavigationDto? ScoredBy { get; set; }
    public required string ScoringModelName { get; set; }
}
