using Wayd.Common.Application.SystemSettings.General.Commands;

namespace Wayd.Web.Api.Models.Admin.SystemSettings;

public sealed record UpdateGeneralSettingsRequest
{
    /// <summary>
    /// The system time zone, as an IANA id such as <c>America/Chicago</c>: the zone a calendar day is read in
    /// wherever no team's own zone applies, and the zone new teams start with.
    /// </summary>
    public string TimeZone { get; set; } = default!;

    public UpdateGeneralSettingsCommand ToUpdateGeneralSettingsCommand() => new(TimeZone);
}

public sealed class UpdateGeneralSettingsRequestValidator : CustomValidator<UpdateGeneralSettingsRequest>
{
    public UpdateGeneralSettingsRequestValidator()
    {
        RuleFor(r => r.TimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}
