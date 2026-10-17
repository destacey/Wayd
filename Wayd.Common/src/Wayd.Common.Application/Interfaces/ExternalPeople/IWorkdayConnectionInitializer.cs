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
/// One Workday organization of a given organization type.
/// <c>reference</c> is the organization's stable Workday WID, used for exclusion matching.
/// <c>displayName</c> is its human-friendly name (or descriptor).
/// <c>referenceId</c> is the tenant-defined business code (e.g. "ENG-001", "COMP-EMEA"), shown as
/// secondary context to tell apart two organizations with the same name; null when the tenant has none.
/// </summary>
/// <remarks>
/// Returned by <see cref="IWorkdayConnectionInitializer.GetOrganizationsByType"/>.
/// </remarks>
public sealed record DiscoveredOrg(string Reference, string? DisplayName, string? ReferenceId = null);

/// <summary>
/// Outcome of the connection's most recent init probe: whether it could authenticate and read the
/// fields Wayd requires, and the organization types it discovered.
/// </summary>
/// <remarks>Persisted on the connection so the UI can render it without re-running the probe.</remarks>
public sealed record ConnectionInitResult(
    bool IsValid,
    int WorkersProbed,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<string> Warnings,
    string? AuthError,
    IReadOnlyList<DiscoveredOrgType>? DiscoveredOrgTypes = null);

/// <summary>
/// One Workday organization type discovered by the connection's init probe, with how many
/// organizations of that type the tenant has.
/// </summary>
/// <remarks>
/// Mirrors the domain <c>WorkdayOrgType</c> record but lives in Common.Application so the
/// initializer interface doesn't take a dependency on AppIntegration.Domain.
/// </remarks>
public sealed record DiscoveredOrgType(string TypeId, string? DisplayName, int Count);
