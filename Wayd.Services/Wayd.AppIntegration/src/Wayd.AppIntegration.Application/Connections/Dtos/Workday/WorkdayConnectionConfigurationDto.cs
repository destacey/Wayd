using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Workday;

public sealed record WorkdayConnectionConfigurationDto : IMapFrom<WorkdayConnectionConfiguration>
{
    /// <summary>The WSDL URL the admin entered. Source of truth for the endpoint.</summary>
    public required string WsdlUrl { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The Workday service host.</summary>
    public required string ServiceHost { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The tenant alias.</summary>
    public required string TenantAlias { get; set; }

    /// <summary>Derived from <see cref="WsdlUrl"/>. The WWS version.</summary>
    public required string WsdlVersion { get; set; }

    /// <summary>The Integration System User username.</summary>
    public required string IsuUsername { get; set; }

    /// <summary>
    /// The Integration System User password.
    /// </summary>
    /// <remarks>This will be masked when returned from the API.</remarks>
    public required string IsuPassword { get; set; }

    /// <summary>Which Workday worker identifier is used as the upsert key.</summary>
    public WorkdayWorkerKey WorkerKey { get; set; }

    /// <summary>When true, terminated/inactive workers are also returned by the sync.</summary>
    public bool IncludeInactive { get; set; }

    /// <summary>Which uniquely-indexed Employee field the sync upsert matches on.</summary>
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
    /// Workday <c>Organization_Type_ID</c> that drives <c>Employee.Department</c>. Null means the
    /// admin opted out of Department sync.
    /// </summary>
    public string? DepartmentOrganizationTypeId { get; set; }

    /// <summary>
    /// Catalogue of org-types discovered during the most recent init probe. Drives the
    /// admin-facing dropdown for <see cref="DepartmentOrganizationTypeId"/>.
    /// </summary>
    public List<WorkdayOrgTypeDto>? DiscoveredOrgTypes { get; set; }

    /// <summary>
    /// Admin-configured rules that filter workers out of the sync. Each rule names an
    /// Organization_Type_ID plus the WID of an org of that type; workers whose
    /// Worker_Organization_Data references that WID are dropped before upsert. Empty by default.
    /// </summary>
    public List<WorkdayOrgExclusionDto> OrgExclusions { get; set; } = [];

    // --- Init / probe result ---

    /// <summary>UTC timestamp of the most recent init probe.</summary>
    public DateTimeOffset? LastInitAt { get; set; }

    /// <summary>Whether the most recent init probe succeeded.</summary>
    public bool LastInitSucceeded { get; set; }

    /// <summary>Required fields the ISU could not read during the most recent probe.</summary>
    public List<string>? LastInitMissingFields { get; set; }

    /// <summary>Non-fatal observations from the most recent probe.</summary>
    public List<string>? LastInitWarnings { get; set; }

    /// <summary>Authentication error from the most recent probe, if applicable.</summary>
    public string? LastInitAuthError { get; set; }

    /// <summary>Same masking pattern as Entra's MaskClientSecret — preserves first 4 chars and length.</summary>
    public void MaskIsuPassword()
    {
        if (!string.IsNullOrWhiteSpace(IsuPassword) && IsuPassword.Length > 4)
            IsuPassword = string.Concat(IsuPassword.AsSpan(0, 4), new string('*', IsuPassword.Length - 4));
    }
}

/// <summary>One entry in the org-type catalog discovered by the init probe.</summary>
public sealed record WorkdayOrgTypeDto(string TypeId, string? DisplayName, int Count) : IMapFrom<WorkdayOrgType>;

/// <summary>One admin-configured exclusion rule for the Workday sync.</summary>
public sealed record WorkdayOrgExclusionDto(
    string OrganizationTypeId,
    string OrganizationReference,
    string? DisplayName) : IMapFrom<WorkdayOrgExclusion>;
