using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Web.Api.Models.Work.Sprints;

/// <summary>
/// Replaces a sprint's team days off.
/// </summary>
public sealed record SetSprintTeamDaysOffRequest
{
    /// <summary>
    /// The days within the sprint's planned dates that the whole team is off for some or all of, such as an
    /// offsite, each listed once. An empty list clears them. Holidays from the team's calendar and days outside
    /// its working week need not be listed.
    /// </summary>
    public List<SprintTeamDayOffRequest> TeamDaysOff { get; set; } = [];

    /// <summary>The days as the command takes them.</summary>
    public IReadOnlyList<TeamDayOff> ToTeamDaysOff() => [.. TeamDaysOff.Select(d => new TeamDayOff(d.Date, d.PercentOff))];
}

public sealed class SetSprintTeamDaysOffRequestValidator : CustomValidator<SetSprintTeamDaysOffRequest>
{
    public SetSprintTeamDaysOffRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.TeamDaysOff)
            .NotNull()
            .Must(days => days.Where(d => d is not null).Select(d => d.Date).Distinct().Count() == days.Count(d => d is not null))
                .WithMessage("Each team day off can be listed only once.");

        RuleForEach(r => r.TeamDaysOff)
            .NotNull()
                .WithMessage("Each team day off must be a day with a date.")
            .Must(d => TeamDayOff.IsValidPercent(d.PercentOff))
                .WithMessage("A team day off must be off for 1 to 100 percent of the day.");
    }
}

/// <summary>A day the whole team is off for some or all of.</summary>
public sealed record SprintTeamDayOffRequest
{
    /// <summary>The day, within the sprint's planned dates.</summary>
    public LocalDate Date { get; set; }

    /// <summary>
    /// The share of the working day the team is off, from 1 to 100; 100, a full day, when left out. A half-day
    /// offsite is 50: it takes half of everyone's capacity that day, and the ideal burn-down drops by half a day.
    /// </summary>
    public int PercentOff { get; set; } = TeamDayOff.FullDay;
}
