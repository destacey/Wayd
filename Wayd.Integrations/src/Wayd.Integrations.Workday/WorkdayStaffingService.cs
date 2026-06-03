using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;
using Wayd.Integrations.Workday.Model;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Integrations.Workday;

/// <summary>
/// Bulk-sync implementation of <see cref="IWorkdayEmployeeSource"/>. Pages through Get_Workers,
/// projects each worker into <see cref="WorkdayEmployee"/> via <see cref="WorkerFieldReader"/>,
/// and returns the flat list to the runner.
/// </summary>
public sealed class WorkdayStaffingService(WorkdayStaffingClient client, ILogger<WorkdayStaffingService> logger) : IWorkdayEmployeeSource
{
    // Workday caps Get_Workers Count at 999/page, but per-call latency scales with page size because
    // Workday hydrates each worker server-side (the dominant cost). 200 keeps each call well under the
    // 90s per-attempt resilience timeout (set in Infrastructure ConfigureServices) and pages small
    // enough to overlap cleanly if/when pagination is parallelized.
    private const int SyncPageSize = 200;

    private readonly WorkdayStaffingClient _client = client;
    private readonly ILogger<WorkdayStaffingService> _logger = logger;

    public async Task<Result<WorkdayEmployeeFetchResult>> GetEmployees(WorkdayRequestContext context, CancellationToken cancellationToken)
    {
        try
        {
            var employees = new List<WorkdayEmployee>();

            // Exclusion stats are accumulated per (TypeId, Reference) so the sync log can show a
            // breakdown like "Cost_Center: Contractors → 32". Keyed on the WID since that's what
            // the matcher uses, but we also remember the type + display name so the final detail
            // record can be human-readable without a config join.
            var exclusionRules = context.OrgExclusions ?? [];
            var exclusionCounts = exclusionRules
                .GroupBy(r => r.OrganizationReference, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new ExclusionAccumulator(g.First(), 0), StringComparer.OrdinalIgnoreCase);

            var page = 1;
            int totalPages;

            // Phase timing: separate the Workday round-trip (network + Workday-side response assembly)
            // from local projection (XPath reads + value-object construction). The bulk-sync wall clock
            // is one or the other; logging the split tells us which to invest in rather than guessing.
            // Stopwatch.GetTimestamp/GetElapsedTime times without allocating a Stopwatch instance and
            // keeps sub-millisecond precision — matches the PerformanceBehavior convention.
            double fetchMs = 0;
            double projectMs = 0;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fetchStart = Stopwatch.GetTimestamp();
                var response = await _client.GetWorkers(context, page, SyncPageSize, cancellationToken);
                var pageFetchMs = Stopwatch.GetElapsedTime(fetchStart).TotalMilliseconds;
                fetchMs += pageFetchMs;
                totalPages = response.TotalPages;

                var projectStart = Stopwatch.GetTimestamp();
                foreach (var worker in response.Workers)
                {
                    // Drop workers in any excluded org before projection — projection runs name
                    // normalization, date parsing, etc., and there's no point doing that work for
                    // workers we're about to discard.
                    var matchedExclusion = FindMatchingExclusion(worker, exclusionCounts);
                    if (matchedExclusion is not null)
                    {
                        exclusionCounts[matchedExclusion].Count++;
                        continue;
                    }

                    var projected = TryProject(worker, context.WorkerKey, context.UseUserIdAsEmailFallback, context.UsePreferredName, context.NormalizeNameCasing, context.DepartmentOrganizationTypeId);
                    if (projected is not null)
                        employees.Add(projected);
                }
                var pageProjectMs = Stopwatch.GetElapsedTime(projectStart).TotalMilliseconds;
                projectMs += pageProjectMs;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Workday Get_Workers page {Page}/{TotalPages} returned {Count} workers ({ProjectedCount} after projection, {ExcludedCount} excluded by rules) — fetch {FetchMs:F0}ms, project {ProjectMs:F1}ms.",
                        page, totalPages, response.Workers.Count, employees.Count, exclusionCounts.Values.Sum(v => v.Count), pageFetchMs, pageProjectMs);
                }

                page++;
            } while (page <= totalPages);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Workday Get_Workers complete: {Projected} workers projected across {Pages} page(s) — total fetch {FetchMs:F0}ms, total project {ProjectMs:F1}ms.",
                    employees.Count, totalPages, fetchMs, projectMs);
            }

            EmitNullRateWarnings(employees);

            // Final counts: drop rules that didn't match anything so the sync log only shows rules
            // that actually fired. An admin can still verify their rules are doing what they expect
            // by editing the connection and seeing them in the config UI.
            var counts = exclusionCounts.Values
                .Where(v => v.Count > 0)
                .Select(v => new WorkdayExclusionCount(v.Rule.OrganizationTypeId, v.Rule.OrganizationReference, v.Rule.DisplayName, v.Count))
                .ToList();

            return Result.Success(new WorkdayEmployeeFetchResult(employees, counts));
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogError(ex, "Workday Get_Workers failed for endpoint {Endpoint}.", context.SoapEndpoint);
            return Result.Failure<WorkdayEmployeeFetchResult>($"Workday Get_Workers failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Workday Get_Workers could not reach endpoint {Endpoint}.", context.SoapEndpoint);
            return Result.Failure<WorkdayEmployeeFetchResult>($"Unable to reach Workday: {ex.Message}");
        }
    }

    // The worker's org-reference WIDs. Constant across the run, so it's compiled once and cached by
    // WorkerFieldReader rather than recompiled per worker.
    private const string OrgReferenceWidXPath =
        "wd:Worker_Data/wd:Organization_Data/wd:Worker_Organization_Data/wd:Organization_Reference/wd:ID[@wd:type='WID']";

    /// <summary>
    /// Returns the WID of the first excluded org the worker belongs to, or null when none of the
    /// configured exclusions match. Both sides are case-insensitive: the worker's WIDs come from a
    /// cached compiled XPath, and the exclusion check is an O(1) probe against the
    /// OrdinalIgnoreCase-keyed accumulator dictionary — so the cost is O(orgs-per-worker), not
    /// O(rules × orgs-per-worker). The returned WID is a valid lookup key for the caller's indexer
    /// precisely because that dictionary ignores case. Workday emits WIDs in a single canonical case
    /// but matching case-insensitively is defensive against tenant exports / copy-paste.
    /// </summary>
    private static string? FindMatchingExclusion(XElement worker, Dictionary<string, ExclusionAccumulator> exclusions)
    {
        if (exclusions.Count == 0) return null;

        foreach (var wid in WorkerFieldReader.SelectValues(worker, OrgReferenceWidXPath))
        {
            if (exclusions.ContainsKey(wid))
                return wid;
        }
        return null;
    }

    /// <summary>Mutable accumulator for exclusion counts during a single sync.</summary>
    private sealed class ExclusionAccumulator
    {
        public ExclusionAccumulator(WorkdayOrgExclusion rule, int count)
        {
            Rule = rule;
            Count = count;
        }
        public WorkdayOrgExclusion Rule { get; }
        public int Count { get; set; }
    }

    private WorkdayEmployee? TryProject(XElement worker, WorkdayWorkerKey workerKey, bool useUserIdAsEmailFallback, bool usePreferredName, bool normalizeNameCasing, string? departmentOrganizationTypeId)
    {
        var wid = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkerWid);
        var employeeId = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.EmployeeId);

        // The upsert key choice drives EmployeeNumber. If the admin chose EmployeeId but a worker
        // doesn't have one, we fall back to the WID so the row still syncs — better to capture
        // the person than to silently drop them.
        var employeeNumber = workerKey switch
        {
            WorkdayWorkerKey.EmployeeId => employeeId ?? wid,
            _ => wid,
        };

        if (string.IsNullOrWhiteSpace(employeeNumber))
        {
            _logger.LogWarning("Skipping Workday worker with no usable identifier.");
            return null;
        }

        // Name source: when the admin opted into preferred names, read Preferred_Name_Data first
        // and only fall back to legal when a specific name component is missing. We resolve each
        // part independently so a worker with a preferred first name but no preferred last name
        // gets {preferred first, legal last} — the closest thing to what the HRIS shows.
        var firstName = usePreferredName
            ? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.PreferredFirstName)
              ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.FirstName)
            : WorkerFieldReader.GetValue(worker, WorkerFieldPaths.FirstName);
        var lastName = usePreferredName
            ? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.PreferredLastName)
              ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.LastName)
            : WorkerFieldReader.GetValue(worker, WorkerFieldPaths.LastName);
        var email = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkEmail);

        // Fall back to User_ID when Contact_Data is missing and the admin opted in. User_ID is
        // exposed by the base Public Worker Reports domain, so this is the only way to pick up an
        // email for tenants that don't grant Personal Contact Information to the ISU's ISSG.
        // We still require it to *look* like an email (RFC-ish) — Workday usernames are sometimes
        // codes like "EMP-1234", and we'd rather skip the worker than write garbage to Employee.Email.
        if (string.IsNullOrWhiteSpace(email) && useUserIdAsEmailFallback)
        {
            var userId = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.UserId);
            if (WorkdayConnectionInitializer.LooksLikeEmail(userId))
                email = userId!.Trim();
        }

        // We Guard.Against.NullOrWhiteSpace inside PersonName / EmailAddress; rather than throwing
        // mid-sync, skip the worker and surface a count in the log.
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Skipping Workday worker {EmployeeNumber}: missing required field (firstName/lastName/email).", employeeNumber);
            return null;
        }

        var managerKey = workerKey switch
        {
            WorkdayWorkerKey.EmployeeId => WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerEmployeeId)
                                            ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerWorkerWid),
            _ => WorkerFieldReader.GetValue(worker, WorkerFieldPaths.ManagerWorkerWid),
        };

        // Workday emits the Active flag as "1"/"0" by default but some response shapes /
        // configurations use "true"/"false" (and casing isn't strictly defined). Normalize before
        // comparing rather than pinning to exact lowercase tokens.
        var activeValue = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.Active);
        var isActive = string.Equals(activeValue, "1", StringComparison.Ordinal)
                       || string.Equals(activeValue, "true", StringComparison.OrdinalIgnoreCase);

        // Worker_Type_Reference: prefer the Descriptor attribute (tenant display value), fall back
        // to the Employee_Type_ID code if the descriptor is empty.
        var employeeType =
            WorkerFieldReader.GetAttributeValue(worker, "Descriptor", WorkerFieldPaths.WorkerTypeReference)
            ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.WorkerTypeId);

        var middleName = usePreferredName
            ? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.PreferredMiddleName)
              ?? WorkerFieldReader.GetValue(worker, WorkerFieldPaths.MiddleName)
            : WorkerFieldReader.GetValue(worker, WorkerFieldPaths.MiddleName);

        // Apply title-casing only to all-caps inputs — mixed-case names from the source are
        // preserved unchanged. The helper is a pass-through for null and for already-cased strings,
        // so it's cheap to call unconditionally; we still gate on the flag so the admin can opt out.
        if (normalizeNameCasing)
        {
            firstName = NameCasing.TitleCaseIfMostlyUpper(firstName)!;
            middleName = NameCasing.TitleCaseIfMostlyUpper(middleName);
            lastName = NameCasing.TitleCaseIfMostlyUpper(lastName)!;
        }

        return new WorkdayEmployee(
            employeeNumber: employeeNumber,
            name: new PersonName(firstName, middleName, lastName),
            email: new EmailAddress(email),
            hireDate: ParseWorkdayDate(WorkerFieldReader.GetValue(worker, WorkerFieldPaths.HireDate)),
            jobTitle: WorkerFieldReader.GetValue(worker, WorkerFieldPaths.JobTitle),
            department: ResolveDepartment(worker, departmentOrganizationTypeId),
            officeLocation: WorkerFieldReader.GetValue(worker, WorkerFieldPaths.OfficeLocation),
            managerEmployeeNumber: managerKey,
            isActive: isActive,
            employeeType: employeeType);
    }

    /// <summary>
    /// Reads the worker's department from the supplied <c>Organization_Type_ID</c>. Returns null
    /// when the admin opted out (null/whitespace type ID) or when the worker isn't in any org of
    /// that type. The XPath filter is built from the type ID rather than hard-coded so a customer
    /// whose dept lives under <c>COST_CENTER</c> or a tenant-custom type works the same as the
    /// default <c>SUPERVISORY</c> path.
    /// </summary>
    private static string? ResolveDepartment(XElement worker, string? departmentOrganizationTypeId)
    {
        if (string.IsNullOrWhiteSpace(departmentOrganizationTypeId)) return null;

        // Type ID safety: Workday IDs are alphanumeric + underscore + hyphen + digits per the
        // platform's identifier rules. Reject anything else before interpolating to defuse the
        // XPath-injection risk. If a tenant uses a value outside this character set, the admin
        // has bigger problems than our sync — we surface null and they can pick a different type.
        if (!IsSafeOrgTypeId(departmentOrganizationTypeId)) return null;

        // Match the Microsoft Entra connector's published XPath shape — filter Worker_Organization_Data
        // by Organization_Type_ID and read Organization_Name from the matched node.
        var xpath = $"wd:Worker_Data/wd:Organization_Data/wd:Worker_Organization_Data[wd:Organization_Data/wd:Organization_Type_Reference/wd:ID[@wd:type='Organization_Type_ID']='{departmentOrganizationTypeId}']/wd:Organization_Data/wd:Organization_Name";
        return WorkerFieldReader.GetValue(worker, xpath);
    }

    /// <summary>
    /// Whitelist of characters allowed in a Workday Organization_Type_ID. Standard system types
    /// are uppercase + underscore (SUPERVISORY, COST_CENTER, BUSINESS_UNIT). Tenant-custom types
    /// follow patterns like ORGANIZATION_TYPE-3-55 — letters, digits, underscores, and hyphens.
    /// </summary>
    private static bool IsSafeOrgTypeId(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not ('_' or '-'))
                return false;
        }
        return true;
    }

    private static Instant? ParseWorkdayDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Workday emits ISO 8601 dates: yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ depending on the field.
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return Instant.FromDateTimeOffset(dto);
        return null;
    }

    /// <summary>
    /// Observability hook: when a typically-populated field comes back null on more than half of
    /// the workers in a single run, something on the Workday side probably changed (deprecated
    /// field path, schema drift). Log it so we notice before customers do.
    /// </summary>
    private void EmitNullRateWarnings(List<WorkdayEmployee> employees)
    {
        if (employees.Count == 0) return;

        var halfThreshold = employees.Count / 2;

        // Active is the canary. If literally every projected worker is inactive, the most likely
        // cause is an XPath mismatch against the tenant's actual envelope shape (different element
        // name, namespace, or nesting) rather than 100% of the workforce being terminated.
        var inactiveCount = employees.Count(e => !e.IsActive);
        if (inactiveCount == employees.Count && employees.Count > 1)
            _logger.LogWarning(
                "Workday sync: {InactiveCount}/{Total} workers projected as IsActive=false — every worker came back inactive, which usually indicates the Worker_Status_Data/Active XPath isn't matching this tenant's envelope shape. Enable Debug logging on Wayd.Integrations.Workday to see the raw per-worker value.",
                inactiveCount, employees.Count);

        var nullJobTitle = employees.Count(e => string.IsNullOrEmpty(e.JobTitle));
        if (nullJobTitle > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have a null JobTitle — verify the response group / field path is still valid.", nullJobTitle, employees.Count);

        var nullDepartment = employees.Count(e => string.IsNullOrEmpty(e.Department));
        if (nullDepartment > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have a null Department — verify the response group / field path is still valid.", nullDepartment, employees.Count);

        var nullManager = employees.Count(e => string.IsNullOrEmpty(e.ManagerEmployeeNumber));
        if (nullManager > halfThreshold)
            _logger.LogWarning("Workday sync: {NullCount}/{Total} workers have no Manager reference — verify Include_Management_Chain_Data is set and the chain XPath still matches the tenant's response shape.", nullManager, employees.Count);
    }
}
