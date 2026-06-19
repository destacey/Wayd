namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Fetches workers from a specific Workday tenant identified by the supplied
/// <see cref="WorkdayRequestContext"/>. Each call builds its own SOAP client — there is no
/// shared per-process tenant configuration.
/// </summary>
public interface IWorkdayEmployeeSource
{
    Task<Result<WorkdayEmployeeFetchResult>> GetEmployees(WorkdayRequestContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a Workday <c>Get_Workers</c> fetch. Carries the projected employees plus a per-rule
/// breakdown of how many workers each <c>OrgExclusions</c> rule filtered out, so the runner can
/// surface the count in the sync run's detail JSON. An empty <see cref="ExclusionCounts"/> means
/// either no exclusion rules were configured or no workers matched any rule.
/// </summary>
public sealed record WorkdayEmployeeFetchResult(
    IReadOnlyList<IExternalEmployee> Employees,
    IReadOnlyList<WorkdayExclusionCount> ExclusionCounts);

/// <summary>
/// One row in the exclusion breakdown: the rule that fired and how many workers it dropped. The
/// label is built at filter time from <c>OrganizationTypeId</c> + <c>DisplayName</c> so the sync
/// log can read e.g. <c>"Cost_Center: Contractors → 32"</c> without joining back to config.
/// </summary>
public sealed record WorkdayExclusionCount(
    string OrganizationTypeId,
    string OrganizationReference,
    string? DisplayName,
    int Count);
