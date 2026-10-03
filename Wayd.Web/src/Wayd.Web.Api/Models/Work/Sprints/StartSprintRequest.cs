namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Starts a sprint.
/// </summary>
public sealed record StartSprintRequest
{
    /// <summary>
    /// The team's open sprint, named to confirm completing it at the same moment. A team has one open sprint
    /// at a time, so starting is refused while another is open unless this names it — and refused if it names
    /// a sprint that is no longer the open one.
    /// </summary>
    public Guid? CompleteOpenSprintId { get; set; }

    /// <summary>
    /// When the team started the sprint, now or earlier. Omit to record it as starting now. It must fall in
    /// the sprint's start window, which the sprint details report.
    /// </summary>
    public Instant? StartedAt { get; set; }
}
