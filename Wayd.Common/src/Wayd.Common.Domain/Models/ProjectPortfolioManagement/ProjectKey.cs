using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Extensions.ProjectPortfolioManagement;

namespace Wayd.Common.Domain.Models.ProjectPortfolioManagement;

/// <summary>
/// Represents a unique project key used for task key generation (e.g., "APOLLO", "MARS1").
/// Must be 2-20 uppercase alphanumeric characters.
/// </summary>
public sealed class ProjectKey : ValueObject
{
    public const string Regex = "^([A-Z0-9]){2,20}$";

    public ProjectKey(string value)
    {
        value = Guard.Against.NullOrWhiteSpace(value, nameof(ProjectKey)).Trim().ToUpper();

        ValidateFormat(value);

        Value = value;
    }

    public string Value { get; }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Value;
    }

    private bool ValidateFormat(string value)
    {
        return value.IsValidProjectKeyFormat()
            ? true
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(ProjectKey));
    }

    public static implicit operator string(ProjectKey projectKey) => projectKey.Value;
    public static explicit operator ProjectKey(string value) => new(value);
}