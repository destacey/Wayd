using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Scoring.Dtos;

/// <summary>
/// The denormalised current-score summary for a project, surfaced on list/grid DTOs so the score, its
/// date, who set it, and the model can be shown without joining to the score history. The scorer's name
/// is resolved by a join at projection time (not frozen) so it always reflects the current employee.
/// </summary>
public sealed record ScoreSummaryDto
{
    public decimal Value { get; set; }
    public Instant ScoredOn { get; set; }

    /// <summary>
    /// The employee who recorded the score, resolved by navigation so the name is always current
    /// (not frozen). May be null if the scorer is not found.
    /// </summary>
    public EmployeeNavigationDto? ScoredBy { get; set; }
    public required string ScoringModelName { get; set; }
}
