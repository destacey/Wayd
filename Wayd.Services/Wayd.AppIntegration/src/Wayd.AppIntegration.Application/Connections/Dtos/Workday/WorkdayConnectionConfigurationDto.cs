using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Workday;

public sealed record WorkdayConnectionConfigurationDto : IMapFrom<WorkdayConnectionConfiguration>
{
    /// <summary>
    /// The Workday Web Services WSDL URL entered for the connection. The service host, tenant alias,
    /// and version are derived from it.
    /// </summary>
    public required string WsdlUrl { get; set; }

    /// <summary>The Workday service host, derived from <c>wsdlUrl</c>.</summary>
    public required string ServiceHost { get; set; }

    /// <summary>The Workday tenant alias, derived from <c>wsdlUrl</c>.</summary>
    public required string TenantAlias { get; set; }

    /// <summary>The Workday Web Services (WWS) version, derived from <c>wsdlUrl</c>.</summary>
    public required string WsdlVersion { get; set; }

    /// <summary>The username of the Workday Integration System User (ISU) the connection signs in as.</summary>
    public required string IsuUsername { get; set; }

    /// <summary>
    /// The Integration System User password. Returned masked as a fixed-width placeholder.
    /// </summary>
    public required string IsuPassword { get; set; }

    /// <summary>Which Workday worker identifier the sync uses to identify each worker.</summary>
    /// <remarks>Used as the upsert key.</remarks>
    public WorkdayWorkerKey WorkerKey { get; set; }

    /// <summary>When true, terminated/inactive workers are also returned by the sync.</summary>
    public bool IncludeInactive { get; set; }

    /// <summary>The employee field the sync uses to match a worker to an existing employee.</summary>
    /// <remarks>Each option is a uniquely-indexed Employee field; the sync upsert matches on it.</remarks>
    public EmployeeMatchProperty MatchBy { get; set; }

    /// <summary>
    /// When true, the probe and sync accept Workday's <c>User_ID</c> as the work email when
    /// <c>Contact_Data</c> is missing, provided it parses as a valid email address.
    /// </summary>
    public bool UseUserIdAsEmailFallback { get; set; }

    /// <summary>
    /// When true, sync reads <c>Preferred_Name_Data</c> in preference to <c>Legal_Name_Data</c>.
    /// </summary>
    public bool UsePreferredName { get; set; }

    /// <summary>
    /// When true, names that come back from Workday in all-caps are title-cased before storage.
    /// </summary>
    public bool NormalizeNameCasing { get; set; }

    /// <summary>
    /// Workday <c>Organization_Type_ID</c> whose organization sets each employee's department.
    /// Null when department sync is turned off.
    /// </summary>
    public string? DepartmentOrganizationTypeId { get; set; }

    /// <summary>
    /// The organization types discovered during the most recent init probe; the choices for
    /// <c>departmentOrganizationTypeId</c>.
    /// </summary>
    public List<WorkdayOrgTypeDto>? DiscoveredOrgTypes { get; set; }

    /// <summary>
    /// Rules that filter workers out of the sync. Each rule names an <c>Organization_Type_ID</c>
    /// plus the WID of an organization of that type; workers whose <c>Worker_Organization_Data</c>
    /// references that WID are excluded from the sync. Empty by default.
    /// </summary>
    /// <remarks>Excluded workers are dropped before upsert.</remarks>
    public List<WorkdayOrgExclusionDto> OrgExclusions { get; set; } = [];

    // --- Init / probe result ---

    /// <summary>UTC timestamp of the most recent init probe; null when no probe has run.</summary>
    public DateTimeOffset? LastInitAt { get; set; }

    /// <summary>Whether the most recent init probe succeeded.</summary>
    public bool LastInitSucceeded { get; set; }

    /// <summary>Required fields the ISU could not read during the most recent probe.</summary>
    public List<string>? LastInitMissingFields { get; set; }

    /// <summary>Non-fatal observations from the most recent probe.</summary>
    public List<string>? LastInitWarnings { get; set; }

    /// <summary>
    /// The authentication error the most recent probe reported. Null when it reported none, which does not mean
    /// the credentials are valid: a probe that could not reach Workday, timed out or failed for another reason
    /// also leaves it null. Read it with <c>lastInitSucceeded</c>.
    /// </summary>
    public string? LastInitAuthError { get; set; }

    /// <summary>Replaces the ISU password with the fixed-width placeholder. See <see cref="ConnectionSecret"/>.</summary>
    public void MaskIsuPassword()
        => IsuPassword = ConnectionSecret.Masked(IsuPassword);
}

/// <summary>One entry in the org-type catalog discovered by the init probe.</summary>
public sealed record WorkdayOrgTypeDto(string TypeId, string? DisplayName, int Count) : IMapFrom<WorkdayOrgType>;

/// <summary>One admin-configured exclusion rule for the Workday sync.</summary>
public sealed record WorkdayOrgExclusionDto(
    string OrganizationTypeId,
    string OrganizationReference,
    string? DisplayName) : IMapFrom<WorkdayOrgExclusion>;
