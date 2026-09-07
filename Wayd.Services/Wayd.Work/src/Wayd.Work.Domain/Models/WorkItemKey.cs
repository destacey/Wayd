using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using Wayd.Common.Models;
using Wayd.Common.Serialization;
using Wayd.Work.Domain.Extensions;

namespace Wayd.Work.Domain.Models;

[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public sealed class WorkItemKey : ScalarValueObject<string>
{
    internal const string Regex = "^([A-Z][A-Z0-9]{1,19})-(\\d+)$";

    public WorkItemKey(string value) : base(Validate(value))
    {
    }

    public WorkItemKey(WorkspaceKey workspaceKey, int number) : base(FormatAndValidate(workspaceKey, number))
    {
    }

    private static string Validate(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(WorkItemKey));

        return value.IsValidWorkItemKeyFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(WorkItemKey));
    }

    private static string FormatAndValidate(WorkspaceKey workspaceKey, int number)
    {
        Guard.Against.NullOrWhiteSpace(workspaceKey);

        string value = $"{workspaceKey}-{number}";

        return value.IsValidWorkItemKeyFormat()
            ? value
            : throw new ArgumentException("The value submitted does not meet the required format.", nameof(WorkItemKey));
    }

    public string WorkspaceKey => Value.Split('-')[0];
    public int WorkItemNumber => int.Parse(Value.Split('-')[1]);

    public (WorkspaceKey WorkspaceKey, int Number) Split()
    {
        string[] parts = Value.Split('-');
        return (new WorkspaceKey(parts[0]), int.Parse(parts[1]));
    }

    public static explicit operator WorkItemKey(string value) => new(value);
}
