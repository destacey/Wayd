namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Runs a small probe against a Workday tenant to validate that the connection can authenticate
/// and that the ISU's security group grants access to the fields Wayd requires. Separate from
/// <see cref="IWorkdayEmployeeSource"/> so init probes and full syncs have different acceptance
/// criteria — both reuse the same underlying SOAP client.
/// </summary>
public interface IWorkdayConnectionInitializer
{
    Task<ConnectionInitResult> Initialize(WorkdayRequestContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Lazy-loads the orgs of a single Organization_Type_ID for the admin's exclusion picker.
    /// Backed by <c>Get_Organizations</c> with a type-reference filter so a tenant with thousands
    /// of supervisory orgs doesn't blow the response size. Pagination is bounded by the underlying
    /// SOAP page cap (~50k orgs ceiling).
    /// </summary>
    Task<Result<IReadOnlyList<DiscoveredOrg>>> GetOrganizationsByType(
        WorkdayRequestContext context,
        string organizationTypeId,
        CancellationToken cancellationToken);
}

/// <summary>
/// One organization returned by <see cref="IWorkdayConnectionInitializer.GetOrganizationsByType"/>.
/// <see cref="Reference"/> is the stable Workday WID we use for exclusion matching.
/// <see cref="DisplayName"/> is the human-friendly Name (or Descriptor) for the picker.
/// <see cref="ReferenceId"/> is the tenant-defined business code (e.g. "ENG-001", "COMP-EMEA");
/// surfaced as secondary context so admins can disambiguate two orgs with the same name.
/// </summary>
public sealed record DiscoveredOrg(string Reference, string? DisplayName, string? ReferenceId = null);

/// <summary>Structured outcome of an init probe. Persisted on the connection so the UI can render it without re-running.</summary>
public sealed record ConnectionInitResult(
    bool IsValid,
    int WorkersProbed,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> Warnings,
    string? AuthError,
    IReadOnlyList<DiscoveredOrgType>? DiscoveredOrgTypes = null);

/// <summary>
/// One entry in the org-type catalog discovered by the init probe. Mirrors the domain
/// <c>WorkdayOrgType</c> record but lives in Common.Application so the initializer interface
/// doesn't take a dependency on AppIntegration.Domain.
/// </summary>
public sealed record DiscoveredOrgType(string TypeId, string? DisplayName, int Count);
