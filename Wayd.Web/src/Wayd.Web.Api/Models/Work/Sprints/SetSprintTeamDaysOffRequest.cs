namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Replaces a sprint's team days off.
/// </summary>
public sealed record SetSprintTeamDaysOffRequest
{
    /// <summary>
    /// The days within the sprint's planned dates that the whole team is off, such as an offsite. An empty list
    /// clears them. Holidays from the team's calendar and days outside its working week need not be listed.
    /// </summary>
    public List<LocalDate> TeamDaysOff { get; set; } = [];
}
