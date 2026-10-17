using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// A summary of a project's current score: the score, when it was recorded, who recorded it, and the
/// scoring model used.
/// </summary>
/// <remarks>
/// Denormalised onto the project so it can be shown without joining to the score history.
/// </remarks>
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
