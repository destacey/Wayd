namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Corrects the actual start and completion of one or more of a team's sprints.
/// </summary>
public sealed record CorrectSprintActualDatesRequest
{
    /// <summary>
    /// The sprints to correct, all of one team. Correct neighbouring sprints together when moving one past the
    /// other.
    /// </summary>
    public List<SprintActualDatesRequest> Sprints { get; set; } = [];
}

/// <summary>
/// A sprint's corrected actual dates. Both values are replaced; omit one to revert it to the sprint's default.
/// </summary>
public sealed record SprintActualDatesRequest
{
    public Guid SprintId { get; set; }

    /// <summary>When the team actually started the sprint. Omit to follow the planned start.</summary>
    public Instant? Started { get; set; }

    /// <summary>When the team actually completed the sprint. Omit to follow the planned end.</summary>
    public Instant? Completed { get; set; }
}
