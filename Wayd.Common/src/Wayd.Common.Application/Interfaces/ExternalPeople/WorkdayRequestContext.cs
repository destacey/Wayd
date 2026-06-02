using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// ISU auth credentials for the Workday Staffing service. WS-Security UsernameToken is the only
/// auth mode we use today; the SOAP client builds the header from this directly.
/// </summary>
public sealed record WorkdayCredentials(string IsuUsername, string IsuPassword);

/// <summary>
/// Per-call context for the Workday Staffing service: endpoint + auth + behavior flags +
/// admin-configured filters. Built fresh from the persisted <c>WorkdayConnection</c> on every
/// request — there's no shared per-process tenant state. Naming intentionally avoids
/// "Credentials" because most of the fields aren't credentials.
/// </summary>
public sealed record WorkdayRequestContext(
    string SoapEndpoint,
    string TenantAlias,
    string WsdlVersion,
    WorkdayCredentials Credentials,
    WorkdayWorkerKey WorkerKey,
    bool IncludeInactive,
    Instant? IncrementalUpdatedFrom,
    bool UseUserIdAsEmailFallback = false,
    bool UsePreferredName = false,
    bool NormalizeNameCasing = true,
    string? DepartmentOrganizationTypeId = null,
    IReadOnlyList<WorkdayOrgExclusion>? OrgExclusions = null);

/// <summary>
/// Exclusion rule passed to the SOAP service: drop any worker whose Worker_Organization_Data
/// includes a reference to <see cref="OrganizationReference"/>. <see cref="OrganizationTypeId"/>
/// is carried for the sync-detail breakdown so the run log can show why workers were filtered out.
/// </summary>
public sealed record WorkdayOrgExclusion(
    string OrganizationTypeId,
    string OrganizationReference,
    string? DisplayName);
