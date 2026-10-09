using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Web.Api.Models.Organizations.Teams;

public sealed record UpdateTeamOperatingModelRequest
{
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
    /// The days of the week the team works. At least one. Omit to keep the model's working week.
    /// </summary>
    public List<IsoDayOfWeek>? WorkingDays { get; set; }

    /// <summary>
    /// The holiday calendar whose holidays the team takes off, or null for the system default calendar.
    /// </summary>
    public Guid? HolidayCalendarId { get; set; }

    public UpdateTeamOperatingModelCommand ToUpdateTeamOperatingModelCommand(Guid teamId, Guid operatingModelId)
    {
        return new UpdateTeamOperatingModelCommand(teamId, operatingModelId, Methodology, SizingMethod, TimeZone, CommitmentGraceDays, WorkingDays, HolidayCalendarId);
    }
}

public sealed class UpdateTeamOperatingModelRequestValidator : CustomValidator<UpdateTeamOperatingModelRequest>
{
    public UpdateTeamOperatingModelRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

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
    }
}
