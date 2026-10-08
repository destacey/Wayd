using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Common.Application.SystemSettings.Scheduling.Commands;

namespace Wayd.Web.Api.Models.Admin.SystemSettings;

public sealed record UpdateSchedulingSettingsRequest
{
    /// <summary>The IANA time zone id new team operating models start with.</summary>
    public string DefaultTimeZone { get; set; } = default!;

    /// <summary>
    /// Days after a sprint's planned start that its commitment is taken when the team does not start it.
    /// </summary>
    public int DefaultCommitmentGraceDays { get; set; }

    /// <summary>The days of the week new team operating models work. At least one.</summary>
    public List<IsoDayOfWeek> DefaultWorkingDays { get; set; } = [];

    /// <summary>
    /// The holiday calendar of every team operating model that has none of its own, or null for none.
    /// </summary>
    public Guid? DefaultHolidayCalendarId { get; set; }

    public UpdateSchedulingSettingsCommand ToUpdateSchedulingSettingsCommand() =>
        new(DefaultTimeZone, DefaultCommitmentGraceDays, DefaultWorkingDays, DefaultHolidayCalendarId);
}

public sealed class UpdateSchedulingSettingsRequestValidator : CustomValidator<UpdateSchedulingSettingsRequest>
{
    public UpdateSchedulingSettingsRequestValidator()
    {
        RuleFor(r => r.DefaultTimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .IsIanaTimeZone();

        RuleFor(r => r.DefaultCommitmentGraceDays)
            .InclusiveBetween(0, SchedulingSettingsValidator.MaxCommitmentGraceDays);

        RuleFor(r => r.DefaultWorkingDays)
            .IsWorkingWeek();
    }
}
