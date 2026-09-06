using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using Wayd.Common.Models;
using Wayd.Common.Serialization;

namespace Wayd.Common.Domain.Models.ProjectPortfolioManagement;

/// <summary>
/// Represents a unique identifier for a project task in the format {ProjectKey}-{Number}.
/// </summary>
[JsonConverter(typeof(ScalarValueObjectJsonConverterFactory))]
public sealed class ProjectTaskKey : ScalarValueObject<string>
{
    internal const string ValidationRegex = "^([A-Z0-9]{2,30})-(\\d+)$";

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTaskKey"/> class from a string value.
    /// </summary>
    /// <param name="value">The task key value (e.g., "APOLLO-1").</param>
    public ProjectTaskKey(string value) : base(Validate(value))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTaskKey"/> class from a project key and task number.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="taskNumber">The task number.</param>
    public ProjectTaskKey(ProjectKey projectKey, int taskNumber) : base(FormatAndValidate(projectKey, taskNumber))
    {
    }

    private static string Validate(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        if (!Regex.IsMatch(value, ValidationRegex))
        {
            throw new ArgumentException($"The value '{value}' does not meet the required format for a project task key. Expected format: PROJECT-###", nameof(value));
        }

        return value;
    }

    private static string FormatAndValidate(ProjectKey projectKey, int taskNumber)
    {
        Guard.Against.Null(projectKey, nameof(projectKey));
        Guard.Against.NegativeOrZero(taskNumber, nameof(taskNumber));

        string value = $"{projectKey.Value}-{taskNumber}";

        if (!Regex.IsMatch(value, ValidationRegex))
        {
            throw new ArgumentException($"The project key '{projectKey.Value}' does not meet the required format. Must be 2-30 uppercase alphanumeric characters or hyphens.", nameof(projectKey));
        }

        return value;
    }

    private int LastHyphenIndex
    {
        get
        {
            var index = Value.LastIndexOf('-');
            if (index < 0)
                throw new InvalidOperationException($"Invalid project task key '{Value}'.");
            return index;
        }
    }

    /// <summary>
    /// Gets the project key portion of the task key (e.g., "APOLLO").
    /// </summary>
    public string ProjectKey => Value[..LastHyphenIndex];

    /// <summary>
    /// Gets the task number portion of the task key (e.g., 1 from "APOLLO-1").
    /// </summary>
    public int TaskNumber => int.Parse(Value[(LastHyphenIndex + 1)..]);

    public static explicit operator ProjectTaskKey(string value) => new(value);
}
