namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Starts a sprint now.
/// </summary>
public sealed record StartSprintRequest
{
    /// <summary>
    /// Confirms completing the team's open sprint at the same instant. A team has one open sprint at a
    /// time, so starting is refused while another is open unless this is set.
    /// </summary>
    public bool CompleteOpenSprint { get; set; }
}
