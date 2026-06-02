using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalPeople;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Workday.Soap;

namespace Wayd.Integrations.Workday;

/// <summary>
/// Init/probe implementation. Pulls a small page (~10 workers) via Get_Workers, inspects
/// element presence to detect ISSG permission gaps, and reports a structured result. Does not
/// write to the Employees table.
/// </summary>
public sealed class WorkdayConnectionInitializer(WorkdayStaffingClient client, ILogger<WorkdayConnectionInitializer> logger) : IWorkdayConnectionInitializer
{
    private const int ProbeSampleSize = 10;
    // Workday caps Get_Organizations at 999/page. We use the max because a single round-trip is
    // cheaper than paging — but we still paginate up to OrgCatalogPageCap pages because Workday
    // returns orgs in no particular type order, so a tenant with thousands of supervisory orgs
    // can hide Cost_Center / Company / custom types on page 2+.
    private const int OrgCatalogPageSize = 999;
    // Safety cap on pages walked during catalog discovery. At 999/page, 50 pages = ~50k orgs —
    // far past any tenant we've seen. Beyond this we log and stop rather than chasing a runaway
    // response (could happen if a tenant returns a misconfigured Total_Pages or pagination loop).
    private const int OrgCatalogPageCap = 50;

    private readonly WorkdayStaffingClient _client = client;
    private readonly ILogger<WorkdayConnectionInitializer> _logger = logger;

    public async Task<ConnectionInitResult> Initialize(WorkdayRequestContext context, CancellationToken cancellationToken)
    {
        try
        {
            // Probe is always a full-snapshot fetch — we want a representative slice, not a delta.
            var probeContext = context with { IncrementalUpdatedFrom = null };
            var response = await _client.GetWorkers(probeContext, page: 1, pageSize: ProbeSampleSize, cancellationToken);

            var workers = response.Workers;
            if (workers.Count == 0)
            {
                return new ConnectionInitResult(
                    IsValid: false,
                    WorkersProbed: 0,
                    MissingRequiredFields: [],
                    Warnings: ["Workday returned zero workers. Verify the ISU's group includes at least one populated supervisory organization."],
                    AuthError: null);
            }

            var requiredFields = BuildRequiredFieldChecks(context.WorkerKey);

            // A field is "missing" only when its element is absent from EVERY sampled worker.
            // Present-but-empty or present-on-some is data variance, not a permission gap.
            var missing = new List<string>();
            foreach (var (label, candidates) in requiredFields)
            {
                bool anyHas;
                if (label == WorkEmailLabel)
                {
                    // Work Email gets the User_ID fallback path when the admin opted in. Either a
                    // proper Contact_Data email OR a valid-looking User_ID counts as "present".
                    anyHas = workers.Any(w => HasWorkEmail(w, context.UseUserIdAsEmailFallback));
                }
                else
                {
                    anyHas = workers.Any(w => WorkerFieldReader.HasElement(w, candidates));
                }
                if (!anyHas)
                    missing.Add(label);
            }

            var warnings = BuildWarnings(workers, context.UseUserIdAsEmailFallback, context.DepartmentOrganizationTypeId);

            // Discover the tenant's org-type catalog so the admin can pick which type drives
            // Department. Failure here is non-fatal: a customer whose ISU isn't granted
            // Organization read can still sync workers; they just lose the Department picker.
            var (discoveredOrgTypes, orgCatalogWarning) = await TryDiscoverOrgTypes(context, cancellationToken);
            if (orgCatalogWarning is not null)
                warnings = [.. warnings, orgCatalogWarning];

            var isValid = missing.Count == 0;
            return new ConnectionInitResult(
                IsValid: isValid,
                WorkersProbed: workers.Count,
                MissingRequiredFields: missing,
                Warnings: warnings,
                AuthError: null,
                DiscoveredOrgTypes: discoveredOrgTypes);
        }
        catch (WorkdaySoapException ex) when (ex.IsAuthFailure)
        {
            _logger.LogWarning(ex, "Workday init probe failed authentication for endpoint {Endpoint}.", context.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [],
                AuthError: $"Authentication failed: {ex.Message}");
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogWarning(ex, "Workday init probe returned an error for endpoint {Endpoint}.", context.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [$"Workday returned an error: {ex.Message}"],
                AuthError: null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Workday init probe could not reach endpoint {Endpoint}.", context.SoapEndpoint);
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: [$"Unable to reach Workday at {context.SoapEndpoint}: {ex.Message}"],
                AuthError: null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionInitResult(
                IsValid: false,
                WorkersProbed: 0,
                MissingRequiredFields: [],
                Warnings: ["Workday did not respond within the timeout. Verify the endpoint URL is correct and the tenant is reachable."],
                AuthError: null);
        }
    }

    /// <summary>
    /// Calls <c>Get_Organizations</c> and groups every org by its <c>Organization_Type_ID</c>.
    /// Returns the catalog plus an optional warning when the call failed (typically an ISSG gap on
    /// the Workday domain "Organizations and Roles: View"). A failure here is intentionally soft —
    /// it doesn't fail the probe, but the admin won't get a Department dropdown.
    /// </summary>
    private async Task<(IReadOnlyList<DiscoveredOrgType> catalog, string? warning)> TryDiscoverOrgTypes(
        WorkdayRequestContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Each org carries an Organization_Type_Reference. The Descriptor attribute is the
            // human-friendly label ("Supervisory Organization"); the inner ID[@type='Organization_Type_ID']
            // is the stable code ("SUPERVISORY"). We collect across pages and group by the stable
            // code, since Workday returns orgs in no particular type order — a big tenant can have
            // 90+ supervisory orgs filling page 1, hiding Cost_Center / Company / custom types
            // on later pages.
            var allEntries = new List<(string TypeId, string? Descriptor)>();
            var page = 1;
            int totalPages;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.GetOrganizations(context, page, OrgCatalogPageSize, cancellationToken);
                totalPages = response.TotalPages;

                foreach (var org in response.Organizations)
                {
                    var typeId = WorkerFieldReader.GetValue(org, OrgTypeIdXPath);
                    if (string.IsNullOrWhiteSpace(typeId)) continue;
                    var descriptor = WorkerFieldReader.GetAttributeValue(org, "Descriptor", OrgTypeReferenceXPath);
                    allEntries.Add((typeId, descriptor));
                }

                page++;
            }
            while (page <= totalPages && page <= OrgCatalogPageCap);

            if (totalPages > OrgCatalogPageCap)
            {
                _logger.LogWarning(
                    "Workday Get_Organizations returned {TotalPages} pages; catalog discovery stopped at the {Cap}-page safety cap. The picker may be missing rare org types from later pages.",
                    totalPages, OrgCatalogPageCap);
            }

            var grouped = allEntries
                .GroupBy(x => x.TypeId, StringComparer.Ordinal)
                .Select(g => new DiscoveredOrgType(
                    TypeId: g.Key,
                    DisplayName: g.Select(x => x.Descriptor).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
                    Count: g.Count()))
                .OrderBy(o => o.TypeId, StringComparer.Ordinal)
                .ToList();

            return (grouped, null);
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogWarning(ex, "Workday Get_Organizations failed; org-type catalog will not be available.");
            return ([], $"Couldn't load the org-type catalog: {ex.Message}. The Department picker will be unavailable until this is resolved (often a grant on the Workday 'Organizations and Roles' domain).");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Workday Get_Organizations failed; org-type catalog will not be available.");
            return ([], $"Couldn't reach Workday to load the org-type catalog: {ex.Message}.");
        }
    }

    // XPath catalog for Get_Organizations response parsing. Lives here rather than in
    // WorkerFieldPaths because these target the Organization shape, not Worker.
    private const string OrgTypeReferenceXPath = "wd:Organization_Data/wd:Organization_Type_Reference";
    private const string OrgTypeIdXPath = OrgTypeReferenceXPath + "/wd:ID[@wd:type='Organization_Type_ID']";

    // Same character whitelist enforced in WorkdayStaffingService.IsSafeOrgTypeId — used here to
    // defuse XPath-injection in the warnings check. Letters / digits / underscore / hyphen.
    private static readonly Regex SafeOrgTypeIdRegex = new(@"^[A-Za-z0-9_\-]+$", RegexOptions.Compiled);

    // The probe's "Work Email" check is special-cased because the User_ID fallback may stand in
    // for a missing Contact_Data email. We refer to it by label rather than identity so the
    // missing-fields report also stays in sync with whatever the customer reads on the UI.
    private const string WorkEmailLabel =
        "Work Email (grant 'Worker Data: Personal Contact Information' to the ISU's ISSG, or enable the User_ID fallback on the connection)";

    private static IReadOnlyList<(string Label, string[] Candidates)> BuildRequiredFieldChecks(WorkdayWorkerKey workerKey)
    {
        var checks = new List<(string, string[])>
        {
            ("Worker WID",   WorkerFieldPaths.WorkerWid),
            ("First Name",   WorkerFieldPaths.FirstName),
            ("Last Name",    WorkerFieldPaths.LastName),
            (WorkEmailLabel, WorkerFieldPaths.WorkEmail),
            ("Active Flag",  WorkerFieldPaths.Active),
        };

        // Employee_ID is only load-bearing when the admin selected it as the upsert key.
        if (workerKey == WorkdayWorkerKey.EmployeeId)
            checks.Add(("Employee ID", WorkerFieldPaths.EmployeeId));

        return checks;
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<XElement> workers, bool useUserIdAsEmailFallback, string? departmentOrganizationTypeId)
    {
        var warnings = new List<string>();

        // Optional fields where "no one has it across 10 workers" is suspicious enough to flag.
        // Department is checked separately because its XPath depends on the configured type ID.
        var optional = new (string Label, string[] Candidates)[]
        {
            ("Job Title",      WorkerFieldPaths.JobTitle),
            ("Manager",        WorkerFieldPaths.ManagerWorkerWid),
        };

        foreach (var (label, candidates) in optional)
        {
            var anyHas = workers.Any(w => WorkerFieldReader.HasElement(w, candidates));
            if (!anyHas)
                warnings.Add($"None of the sampled workers had a {label}. Sync will treat this field as null for all employees.");
        }

        // Department check uses the admin's configured Organization_Type_ID. Skip the warning
        // entirely when the admin opted out (null type ID) — there's no expectation of a value.
        if (!string.IsNullOrWhiteSpace(departmentOrganizationTypeId)
            && SafeOrgTypeIdRegex.IsMatch(departmentOrganizationTypeId))
        {
            var deptXPath = $"wd:Worker_Data/wd:Organization_Data/wd:Worker_Organization_Data[wd:Organization_Data/wd:Organization_Type_Reference/wd:ID[@wd:type='Organization_Type_ID']='{departmentOrganizationTypeId}']/wd:Organization_Data/wd:Organization_Name";
            var anyHasDept = workers.Any(w => WorkerFieldReader.HasElement(w, deptXPath));
            if (!anyHasDept)
                warnings.Add($"None of the sampled workers had a Department under Organization_Type_ID='{departmentOrganizationTypeId}'. Pick a different type from the discovered catalog or leave it unset to skip Department sync.");
        }

        // If the fallback is on AND Contact_Data is genuinely absent on every worker, surface that
        // the sync is leaning on User_ID rather than a real email field. Not an error — just
        // visible so admins know what's happening.
        if (useUserIdAsEmailFallback)
        {
            var anyContactEmail = workers.Any(w => WorkerFieldReader.HasElement(w, WorkerFieldPaths.WorkEmail));
            if (!anyContactEmail)
                warnings.Add("None of the sampled workers had a Contact_Data email; sync is using User_ID as the email source.");
        }

        return warnings;
    }

    /// <summary>
    /// True when the worker has either a Contact_Data work email OR (with fallback on) a User_ID
    /// that parses as a valid email address.
    /// </summary>
    private static bool HasWorkEmail(XElement worker, bool useUserIdAsEmailFallback)
    {
        if (WorkerFieldReader.HasElement(worker, WorkerFieldPaths.WorkEmail))
            return true;
        if (!useUserIdAsEmailFallback)
            return false;
        var userId = WorkerFieldReader.GetValue(worker, WorkerFieldPaths.UserId);
        return LooksLikeEmail(userId);
    }

    /// <summary>
    /// Cheap email-shape check using MailAddress (RFC-5321-ish). Used as a guard so we never pass
    /// a non-email User_ID (e.g. "EMP-1234") through to an EmailAddress constructor.
    /// </summary>
    internal static bool LooksLikeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return MailAddress.TryCreate(value.Trim(), out _);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Paginates up to <see cref="OrgCatalogPageCap"/> pages of 999 — the same bound used by the
    /// catalog discovery loop. Returns every org of the requested type within that ceiling.
    /// </remarks>
    public async Task<Result<IReadOnlyList<DiscoveredOrg>>> GetOrganizationsByType(
        WorkdayRequestContext context,
        string organizationTypeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationTypeId))
            return Result.Failure<IReadOnlyList<DiscoveredOrg>>("organizationTypeId is required.");
        if (!SafeOrgTypeIdRegex.IsMatch(organizationTypeId))
            return Result.Failure<IReadOnlyList<DiscoveredOrg>>("organizationTypeId contains invalid characters.");

        try
        {
            var results = new List<DiscoveredOrg>();
            var page = 1;
            int totalPages;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _client.GetOrganizations(context, page, OrgCatalogPageSize, cancellationToken, organizationTypeId);
                totalPages = response.TotalPages;

                foreach (var org in response.Organizations)
                {
                    // The Organization shape from Get_Organizations: Organization_Reference carries
                    // the WID (used as the stable exclusion-match key); Organization_Data carries
                    // the human-friendly Name. Both go to the picker so the admin sees "Engineering"
                    // but the rule persists "org-aaaa-0001".
                    var wid = WorkerFieldReader.GetValue(org, OrgReferenceWidXPath);
                    if (string.IsNullOrWhiteSpace(wid)) continue;

                    // Pick the best label for the dropdown:
                    //   1. Organization_Data/Name — the human-friendly display name (most tenants).
                    //   2. Organization_Reference/@Descriptor — Workday UI fallback label.
                    //   3. Organization_Data/Reference_ID — the business code (e.g. "COMP-EMEA").
                    // We project all three so the FE can render "Name (ReferenceId)" if both exist
                    // and gracefully fall back to whichever pieces are present.
                    var name = WorkerFieldReader.GetValue(org, OrgNameXPath)
                               ?? WorkerFieldReader.GetAttributeValue(org, "Descriptor", OrgReferenceXPath);
                    var referenceId = WorkerFieldReader.GetValue(org, OrgReferenceIdXPath);

                    results.Add(new DiscoveredOrg(wid, name, referenceId));
                }

                page++;
            }
            while (page <= totalPages && page <= OrgCatalogPageCap);

            return Result.Success<IReadOnlyList<DiscoveredOrg>>(results);
        }
        catch (WorkdaySoapException ex)
        {
            _logger.LogWarning(ex, "Workday Get_Organizations by-type lookup failed for type {TypeId}.", organizationTypeId);
            return Result.Failure<IReadOnlyList<DiscoveredOrg>>($"Couldn't load orgs of type '{organizationTypeId}': {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Workday Get_Organizations by-type lookup could not reach the endpoint.");
            return Result.Failure<IReadOnlyList<DiscoveredOrg>>($"Couldn't reach Workday: {ex.Message}");
        }
    }

    // Get_Organizations response shape (Organization_WWS_DataType): each Organization carries
    //   - Organization_Reference (WID at /wd:ID[@wd:type='WID'])
    //   - Organization_Data/Name (the human-friendly label — NOT Organization_Name, which is the
    //     element name on the *worker* context's Organization_Summary_DataType)
    //   - Organization_Data/Reference_ID (the tenant-defined business code)
    private const string OrgReferenceXPath = "wd:Organization_Reference";
    private const string OrgReferenceWidXPath = OrgReferenceXPath + "/wd:ID[@wd:type='WID']";
    private const string OrgNameXPath = "wd:Organization_Data/wd:Name";
    private const string OrgReferenceIdXPath = "wd:Organization_Data/wd:Reference_ID";
}
