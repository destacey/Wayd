using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.Models.ProjectPortfolioManagement;

/// <summary>
/// Represents a unique identifier for a project task in the format {ProjectKey}-{Number}.
/// </summary>
public sealed class ProjectTaskKey : ValueObject
{
    internal const string ValidationRegex = "^([A-Z0-9]{2,30})-(\\d+)$";

    private ProjectTaskKey() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTaskKey"/> class from a string value.
    /// </summary>
    /// <param name="value">The task key value (e.g., "APOLLO-1").</param>
    public ProjectTaskKey(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));

        if (!ValidateFormat(value))
        {
            throw new ArgumentException($"The value '{value}' does not meet the required format for a project task key. Expected format: PROJECT-###", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjectTaskKey"/> class from a project key and task number.
    /// </summary>
    /// <param name="projectKey">The project key.</param>
    /// <param name="taskNumber">The task number.</param>
    public ProjectTaskKey(ProjectKey projectKey, int taskNumber)
    {
        Guard.Against.Null(projectKey, nameof(projectKey));
        Guard.Against.NegativeOrZero(taskNumber, nameof(taskNumber));

        string value = $"{projectKey.Value}-{taskNumber}";

        if (!ValidateFormat(value))
        {
            throw new ArgumentException($"The project key '{projectKey.Value}' does not meet the required format. Must be 2-30 uppercase alphanumeric characters or hyphens.", nameof(projectKey));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the full task key value (e.g., "APOLLO-1").
    /// </summary>
    public string Value { get; init; } = default!;

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

    /// <summary>
    /// Validates the format of a task key value.
    /// </summary>
    private static bool ValidateFormat(string value)
    {
        return Regex.IsMatch(value, ValidationRegex);
    }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ProjectTaskKey key) => key.Value;
    public static explicit operator ProjectTaskKey(string value) => new(value);
}
