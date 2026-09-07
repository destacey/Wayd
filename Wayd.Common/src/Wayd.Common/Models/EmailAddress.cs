using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Extensions;
using Wayd.Common.Serialization;

namespace Wayd.Common.Models;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public class EmailAddress : ScalarValueObject<string>
{
    /// <summary>
    /// The punctuation an address may carry outside its letters and digits: RFC 5322's <c>atext</c>, plus
    /// the dot that separates atoms and the @ that separates local part from domain.
    /// </summary>
    /// <remarks>
    /// The same set <see cref="StringExtensions.IsValidEmailAddressFormat"/> matches, named here so
    /// anything that has to agree with it can say so rather than restate it. Identity's allowed-username
    /// characters is the case that matters: a username is an address for a locally created account and a
    /// UPN for an Entra one, both address-shaped, so a character accepted as an address and refused as a
    /// username makes the account impossible to create and says nothing useful about why.
    /// </remarks>
    public const string AllowedCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-/=?^_`{|}~.@";

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