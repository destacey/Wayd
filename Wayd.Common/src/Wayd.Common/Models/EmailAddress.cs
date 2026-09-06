using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Serialization;

namespace Wayd.Common.Models;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public class EmailAddress : ScalarValueObject<string>
{
    public EmailAddress(string value) : base(Validate(value))
    {
    }

    private static string Validate(string value)
    {
        value = Guard.Against.NullOrWhiteSpace(value, nameof(EmailAddress)).Trim();

        return value.IsValidEmailAddressFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(EmailAddress));
    }

    public static explicit operator EmailAddress(string value) => new(value);
}