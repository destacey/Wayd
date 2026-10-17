using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Web.Api.Models.Organizations.Teams;

public sealed record SetTeamOperatingModelRequest
{
    /// <summary>
    /// The start date for this operating model.
    /// </summary>
    public LocalDate StartDate { get; set; }

    /// <summary>
    /// The methodology the team uses (e.g., Scrum, Kanban).
    /// </summary>
    public Methodology Methodology { get; set; }

    /// <summary>
    /// The sizing method the team uses (e.g., StoryPoints, Count).
    /// </summary>
    public SizingMethod SizingMethod { get; set; }

    /// <summary>
    /// The IANA id of the time zone the team's days are counted in.
    /// </summary>
    public string TimeZone { get; set; } = default!;

    /// <summary>
    /// Days after a sprint's planned start that its commitment is taken when the team does not start it.
    /// </summary>
    public int CommitmentGraceDays { get; set; }

    /// <summary>
    /// The days of the week the team works. At least one. Omit to carry over the current model's working week.
    /// </summary>
    public List<IsoDayOfWeek>? WorkingDays { get; set; }

    /// <summary>
    /// The holiday calendar whose holidays the team takes off, or null for the system default calendar.
    /// </summary>
    public Guid? HolidayCalendarId { get; set; }

    /// <summary>
    /// The length in weeks of a Kanban team's reporting periods, used for its velocity trend, or null for no cadence.
    /// A Scrum team's sprints are its cadence, so a Scrum model takes none.
    /// </summary>
    public int? CadenceWeeks { get; set; }

    /// <summary>
    /// The first day of one of the cadence's periods, which aligns the others. Required with
    /// <see cref="CadenceWeeks"/>, and omitted without it.
    /// </summary>
    public LocalDate? CadenceAnchor { get; set; }

    public SetTeamOperatingModelCommand ToSetTeamOperatingModelCommand(Guid teamId)
    {
        return new SetTeamOperatingModelCommand(teamId, StartDate, Methodology, SizingMethod, TimeZone, CommitmentGraceDays, WorkingDays, HolidayCalendarId, CadenceWeeks, CadenceAnchor);
    }
}

public sealed class SetTeamOperatingModelRequestValidator : CustomValidator<SetTeamOperatingModelRequest>
{
    public SetTeamOperatingModelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.StartDate)
            .NotEmpty();

        RuleFor(r => r.Methodology)
            .IsInEnum();

        RuleFor(r => r.SizingMethod)
            .IsInEnum();

        RuleFor(r => r.TimeZone)
            .NotEmpty()
            .IsIanaTimeZone();

        RuleFor(r => r.CommitmentGraceDays)
            .InclusiveBetween(0, SchedulingSettingsValidator.MaxCommitmentGraceDays);

        RuleFor(r => r.WorkingDays)
            .IsWorkingWeek()
            .When(r => r.WorkingDays is not null);

        this.RulesForReportingCadence(r => r.Methodology, r => r.CadenceWeeks, r => r.CadenceAnchor);
    }
}
