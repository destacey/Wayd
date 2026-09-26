namespace Wayd.Common.Application.Validation;

public static class TimeZoneValidatorExtension
{
    /// <summary>
    /// Requires an IANA time zone id that NodaTime's bundled tz database knows, which is what every stored
    /// time zone is resolved against. Does not imply <c>NotEmpty()</c>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> IsIanaTimeZone<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(id => id is not null && DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) is not null)
            .WithMessage("'{PropertyValue}' is not a valid IANA time zone.");
}
