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

    public UpdateSchedulingSettingsCommand ToUpdateSchedulingSettingsCommand() =>
        new(DefaultTimeZone, DefaultCommitmentGraceDays);
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
    }
}
