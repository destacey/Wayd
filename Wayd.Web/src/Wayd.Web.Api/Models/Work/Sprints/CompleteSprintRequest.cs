namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Completes a sprint.
/// </summary>
public sealed record CompleteSprintRequest
{
    /// <summary>
    /// When the team completed the sprint, now or earlier. Omit to record it as completing now. It must fall
    /// in the sprint's completion window, which the sprint details report.
    /// </summary>
    public Instant? CompletedAt { get; set; }
}
