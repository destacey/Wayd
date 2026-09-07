using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Domain.Extensions.Organizations;
using Wayd.Common.Models;
using Wayd.Common.Serialization;

namespace Wayd.Common.Domain.Models.Organizations;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public sealed class TeamCode : ScalarValueObject<string>
{
    public const string Regex = "^([A-Z0-9]){2,10}$";

    public TeamCode(string value) : base(Validate(value))
    {
    }

    private static string Validate(string value)
    {
        value = Guard.Against.NullOrWhiteSpace(value, nameof(TeamCode)).Trim().ToUpper();

        return value.IsValidTeamCodeFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(TeamCode));
    }

    public static explicit operator TeamCode(string value) => new(value);
}
