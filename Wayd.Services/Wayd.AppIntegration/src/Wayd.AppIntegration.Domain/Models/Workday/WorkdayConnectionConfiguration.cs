using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Wayd.Common.Domain.DataProtection;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Domain.Models.Workday;

public sealed class WorkdayConnectionConfiguration
{
    [JsonConstructor]
    private WorkdayConnectionConfiguration() { }

    [SetsRequiredMembers]
    public WorkdayConnectionConfiguration(
        string wsdlUrl,
        string isuUsername,
        string isuPassword,
        WorkdayWorkerKey workerKey = WorkdayWorkerKey.EmployeeId,
        bool includeInactive = false,
        EmployeeMatchProperty matchBy = EmployeeMatchProperty.Email,
        bool useUserIdAsEmailFallback = false,
        bool usePreferredName = false,
        bool normalizeNameCasing = true,
        string? departmentOrganizationTypeId = SupervisoryOrgTypeId,
        IReadOnlyList<WorkdayOrgExclusion>? orgExclusions = null)
    {
        WsdlUrl = wsdlUrl.Trim();
        IsuUsername = isuUsername.Trim();
        IsuPassword = isuPassword.Trim();
        WorkerKey = workerKey;
        IncludeInactive = includeInactive;
        MatchBy = matchBy;
        UseUserIdAsEmailFallback = useUserIdAsEmailFallback;
        UsePreferredName = usePreferredName;
        NormalizeNameCasing = normalizeNameCasing;
        DepartmentOrganizationTypeId = string.IsNullOrWhiteSpace(departmentOrganizationTypeId) ? null : departmentOrganizationTypeId.Trim();
        OrgExclusions = orgExclusions?.ToList() ?? [];
        ConfigVersion = 9;

        // Derive endpoint parts at construction so the runtime sync path doesn't reparse on every
        // call. The command-layer validator (BeParseable) rejects unparseable URLs before they
        // reach this constructor, so the throw here is defensive — it documents the invariant
        // (no configuration object exists with an unparseable URL) and protects against any
        // future code path that bypasses the validator (direct seed, new command handler, etc.).
        if (!TryParse(WsdlUrl, out var parsed))
            throw new ArgumentException(
                $"WsdlUrl '{wsdlUrl}' is not a valid Workday Staffing endpoint URL. Expected form: https://{{host}}/ccx/service/{{tenant}}/Staffing/{{version}}.",
                nameof(wsdlUrl));

        ServiceHost = parsed.ServiceHost;
        TenantAlias = parsed.TenantAlias;
        WsdlVersion = parsed.WsdlVersion;
        SoapEndpoint = parsed.SoapEndpoint;
    }

    /// <summary>
    /// Workday's universally-present supervisory-organization type ID. The reporting hierarchy
    /// lives under this type in every Workday tenant. We default <see cref="DepartmentOrganizationTypeId"/>
    /// to this so new connections immediately produce a usable Department value; admins can
    /// override after the init probe surfaces the tenant's full catalog.
    /// </summary>
    public const string SupervisoryOrgTypeId = "SUPERVISORY";

    /// <summary>
    /// The WSDL URL the admin pasted from Workday's "View API Clients" screen — e.g.
    /// <c>https://wd3-impl-services1.workday.com/ccx/service/acme_corp1/Staffing/v46.1?wsdl</c>.
    /// Source of truth for the endpoint; the derived <see cref="ServiceHost"/>,
    /// <see cref="TenantAlias"/>, <see cref="WsdlVersion"/>, and <see cref="SoapEndpoint"/> are
    /// re-computed from this on every save.
    /// </summary>
    public required string WsdlUrl { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The Workday service host (e.g. <c>wd3-impl-services1.workday.com</c>).</summary>
    public required string ServiceHost { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The tenant alias segment.</summary>
    public required string TenantAlias { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The WWS version (e.g. <c>v46.1</c>).</summary>
    public required string WsdlVersion { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The SOAP endpoint URL (the WSDL URL minus the <c>?wsdl</c> suffix).</summary>
    public required string SoapEndpoint { get; set; }

    /// <summary>The Integration System User username (conventionally <c>{user}@{tenant}</c>).</summary>
    public required string IsuUsername { get; set; }

    /// <summary>The Integration System User password. Encrypted at rest.</summary>
    [Encrypted]
    public required string IsuPassword { get; set; }

    /// <summary>Which Workday worker identifier is used as the upsert key.</summary>
    public WorkdayWorkerKey WorkerKey { get; set; }

    /// <summary>When true, terminated/inactive workers are also returned by the sync.</summary>
    public bool IncludeInactive { get; set; }

    /// <summary>
    /// Which uniquely-indexed field on <c>Employee</c> the upsert matches on. Defaults to email
    /// so that connector-switch scenarios (Entra → Workday) naturally collapse onto existing rows.
    /// </summary>
    public EmployeeMatchProperty MatchBy { get; set; }

    /// <summary>
    /// When true, the probe and sync accept <c>Worker_Data/User_ID</c> as the work email when
    /// <c>Personal_Data/Contact_Data/Email_Address_Data</c> is missing — but only if the value
    /// parses as a valid email address. Pragmatic workaround for tenants where the ISU's ISSG
    /// doesn't grant <c>Worker Data: Personal Contact Information</c>. Default off.
    /// </summary>
    public bool UseUserIdAsEmailFallback { get; set; }

    /// <summary>
    /// When true, sync reads <c>Personal_Data/Name_Data/Preferred_Name_Data</c> in preference to
    /// <c>Legal_Name_Data</c>. Falls back to legal when the preferred block is missing or empty.
    /// Default off — legal name is the historical default and matches what most HRIS-driven
    /// reports expect.
    /// </summary>
    public bool UsePreferredName { get; set; }

    /// <summary>
    /// When true, names that come back from Workday in all-caps (a common HRIS convention for
    /// compliance reporting) are title-cased before being stored. Mixed-case input is preserved
    /// untouched. Default on — Workday tenants typically emit upper-cased legal names that look
    /// inconsistent next to manually-entered employees.
    /// </summary>
    public bool NormalizeNameCasing { get; set; }

    /// <summary>
    /// Which Workday <c>Organization_Type_ID</c> drives <c>Employee.Department</c>. Defaults to
    /// <see cref="SupervisoryOrgTypeId"/> ("SUPERVISORY") — Workday's universal reporting-hierarchy
    /// type. Admins can change it to <c>COST_CENTER</c>, <c>BUSINESS_UNIT</c>, a tenant-custom
    /// type ID, or null (don't sync Department at all). The init probe populates
    /// <see cref="DiscoveredOrgTypes"/> with the catalog of valid values for this tenant.
    /// </summary>
    public string? DepartmentOrganizationTypeId { get; set; }

    /// <summary>
    /// Catalogue of organization types present in the customer's tenant, populated by the most
    /// recent successful init probe via <c>Get_Organizations</c>. Drives the admin-facing dropdown
    /// for <see cref="DepartmentOrganizationTypeId"/>. Counts let the admin see at a glance which
    /// types are populated vs empty.
    /// </summary>
    public List<WorkdayOrgType>? DiscoveredOrgTypes { get; set; }

    /// <summary>
    /// Workers belonging to any of these orgs are dropped from the sync after the SOAP response is
    /// parsed. Workday's <c>Get_Workers</c> doesn't expose a server-side exclude filter — there's
    /// only an <c>Organization_Reference</c> include list — so we filter locally. Each rule is a
    /// (type, reference) pair: the type lets the admin understand which org dimension the filter
    /// applies to; the reference is the stable Workday WID we match against the worker's
    /// <c>Worker_Organization_Data</c>. Default empty (no exclusions).
    /// </summary>
    public List<WorkdayOrgExclusion> OrgExclusions { get; set; } = [];

    public int ConfigVersion { get; init; }

    // --- Init / probe result (populated by IWorkdayConnectionInitializer, persisted with config) ---

    /// <summary>UTC timestamp of the most recent init probe.</summary>
    public DateTimeOffset? LastInitAt { get; set; }

    /// <summary>Whether the most recent init probe found a usable configuration.</summary>
    public bool LastInitSucceeded { get; set; }

    /// <summary>Required fields the ISU could not read during the most recent probe (likely an ISSG gap).</summary>
    public List<string>? LastInitMissingFields { get; set; }

    /// <summary>Non-fatal observations from the most recent probe.</summary>
    public List<string>? LastInitWarnings { get; set; }

    /// <summary>Authentication error message from the most recent probe, if the call didn't authenticate at all.</summary>
    public string? LastInitAuthError { get; set; }

    /// <summary>
    /// Parses a Workday WSDL URL into its component parts. Accepts trailing <c>?wsdl</c>.
    /// Returns false on any malformed input — the caller surfaces the failure to the admin.
    /// </summary>
    public static bool TryParse(string? wsdlUrl, out WorkdayWsdlUrlParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(wsdlUrl)) return false;

        if (!Uri.TryCreate(wsdlUrl.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

        // Workday's path shape: /ccx/service/{tenant}/{Service_Name}/{version}
        // Example: /ccx/service/acme_corp1/Staffing/v46.1
        var match = _pathPattern.Match(uri.AbsolutePath);
        if (!match.Success) return false;

        var tenant = match.Groups["tenant"].Value;
        var version = match.Groups["version"].Value;
        var soap = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}{uri.AbsolutePath}";

        parts = new WorkdayWsdlUrlParts(
            ServiceHost: uri.Host,
            TenantAlias: tenant,
            WsdlVersion: version,
            SoapEndpoint: soap);
        return true;
    }

    // The Staffing service is the only one we exercise today; we still match the service name
    // strictly so a misconfigured URL ('Human_Resources' instead of 'Staffing') fails parse rather
    // than silently driving the SOAP client at the wrong endpoint.
    private static readonly Regex _pathPattern = new(
        @"^/ccx/service/(?<tenant>[^/]+)/Staffing/(?<version>v\d+\.\d+)/?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}

public readonly record struct WorkdayWsdlUrlParts(
    string ServiceHost,
    string TenantAlias,
    string WsdlVersion,
    string SoapEndpoint);

/// <summary>
/// One entry in the org-type catalog discovered by the init probe.
/// <see cref="TypeId"/> is the Workday <c>Organization_Type_ID</c> (e.g. "SUPERVISORY", "COST_CENTER",
/// or a tenant-custom ID like "ORGANIZATION_TYPE-3-55"). <see cref="DisplayName"/> is the human-
/// friendly descriptor (e.g. "Supervisory Organization"). <see cref="Count"/> is how many orgs of
/// this type the probe saw — useful as a "this type has data" signal in the admin dropdown.
/// </summary>
public sealed record WorkdayOrgType(string TypeId, string? DisplayName, int Count);

/// <summary>
/// One exclusion rule: drop workers whose <c>Worker_Organization_Data</c> includes a reference to
/// <see cref="OrganizationReference"/>. <see cref="OrganizationTypeId"/> is captured for UI/audit —
/// the matching itself is purely on the WID (which is globally unique across orgs of any type).
/// <see cref="DisplayName"/> is the cached descriptor at the time the rule was created, so the
/// detail page and sync log can show the friendly name without re-fetching from Workday.
/// </summary>
public sealed record WorkdayOrgExclusion(
    string OrganizationTypeId,
    string OrganizationReference,
    string? DisplayName);
