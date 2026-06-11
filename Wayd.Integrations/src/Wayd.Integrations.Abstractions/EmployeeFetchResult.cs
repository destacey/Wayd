using Wayd.Common.Application.Interfaces;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Result of an <see cref="IEmployeeSource.GetEmployees"/> fetch: the projected employees plus a
/// per-rule breakdown of source-side exclusions (how many records each admin-configured rule
/// filtered out before the result was returned). An empty <see cref="ExclusionCounts"/> means
/// either the source has no exclusion rules or none matched.
/// </summary>
public sealed record EmployeeFetchResult(
    IReadOnlyList<IExternalEmployee> Employees,
    IReadOnlyList<EmployeeExclusionCount> ExclusionCounts);

/// <summary>
/// One row in the exclusion breakdown: the rule that fired and how many records it dropped.
/// Connector-neutral projection of source-specific rules (e.g. Workday org exclusions).
/// </summary>
public sealed record EmployeeExclusionCount(
    string RuleType,
    string RuleReference,
    string? DisplayName,
    int Count);
