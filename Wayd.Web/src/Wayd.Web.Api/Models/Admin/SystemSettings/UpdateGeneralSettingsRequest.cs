using Wayd.Common.Application.SystemSettings.General.Commands;

namespace Wayd.Web.Api.Models.Admin.SystemSettings;

/// <summary>The general settings to save, replacing those in effect.</summary>
public sealed record UpdateGeneralSettingsRequest
{
    /// <summary>
    /// The system time zone, as an IANA id such as <c>America/Chicago</c>: the zone a calendar day is read in
    /// wherever no team's own zone applies, and the zone new teams start with.
    /// </summary>
    public string TimeZone { get; set; } = default!;

    /// <summary>The command that saves these values.</summary>
    public UpdateGeneralSettingsCommand ToUpdateGeneralSettingsCommand() => new(TimeZone);
}

/// <summary>Rejects a time zone that is not an IANA id before the request reaches the command.</summary>
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
