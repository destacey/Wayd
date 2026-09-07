using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.Common.Serialization;

namespace Wayd.Common.Domain.Models.ProjectPortfolioManagement;

/// <summary>
/// Represents a unique project key used for task key generation (e.g., "APOLLO", "MARS1").
/// Must be 2-20 uppercase alphanumeric characters.
/// </summary>
[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public sealed class ProjectKey : ScalarValueObject<string>
{
    public const string Regex = "^([A-Z0-9]){2,20}$";

    public ProjectKey(string value) : base(Validate(value))
    {
    }

    private static string Validate(string value)
    {
        value = Guard.Against.NullOrWhiteSpace(value, nameof(ProjectKey)).Trim().ToUpper();

        return value.IsValidProjectKeyFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(ProjectKey));
    }

    public static explicit operator ProjectKey(string value) => new(value);
}