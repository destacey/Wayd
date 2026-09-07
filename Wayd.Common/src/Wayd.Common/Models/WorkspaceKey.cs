using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Serialization;

namespace Wayd.Common.Models;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public sealed class WorkspaceKey : ScalarValueObject<string>
{
    internal const string Regex = "^([A-Z][A-Z0-9]{1,19})$";

    public WorkspaceKey(string value) : base(Validate(value))
    {
    }

    private static string Validate(string value)
    {
        value = Guard.Against.NullOrWhiteSpace(value, nameof(WorkspaceKey)).Trim().ToUpper();

        return value.IsValidWorkspaceKeyFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(WorkspaceKey));
    }

    public static explicit operator WorkspaceKey(string value) => new(value);
}
