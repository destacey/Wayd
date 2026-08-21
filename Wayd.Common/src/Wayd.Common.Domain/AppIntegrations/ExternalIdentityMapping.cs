using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.AppIntegrations;

/// <summary>
/// Links a user in an external delivery system (an Azure DevOps identity, later a Jira account or
/// GitHub user) to a Wayd employee, so synced work can be attributed to a person.
///
/// This is the table <see cref="EmployeeEmail"/> defers to. Those rows are owned by the people
/// connector and reconciled away on every sync; an admin's mapping decision has to outlive that,
/// so it lives here and is keyed on the external system's own stable id rather than an address.
///
/// Scoped to a connection because external ids are only unique within one system — and because
/// the same person can legitimately be a different identity in two organizations.
/// </summary>
public sealed class ExternalIdentityMapping : BaseAuditableEntity
{
    private ExternalIdentityMapping() { }

    private ExternalIdentityMapping(
        Connector connector,
        Guid connectionId,
        string externalId,
        string? email,
        string? displayName,
        string? handle,
        Guid? employeeId,
        ExternalIdentityMappingStatus status,
        Instant lastSeen)
    {
        Connector = connector;
        ConnectionId = connectionId;
        ExternalId = externalId;
        Email = email;
        DisplayName = displayName;
        Handle = handle;
        EmployeeId = employeeId;
        Status = status;
        LastSeen = lastSeen;
    }

    /// <summary>The external system this identity belongs to.</summary>
    public Connector Connector { get; private init; }

    /// <summary>The connection that surfaced this identity.</summary>
    public Guid ConnectionId
    {
        get;
        private init => field = Guard.Against.Default(value, nameof(ConnectionId));
    }

    /// <summary>
    /// The external system's stable identifier — an Azure DevOps identity GUID, a Jira accountId,
    /// a GitHub user id. Immutable: it is this row's identity.
    /// </summary>
    public string ExternalId
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(ExternalId)).Trim();
    } = null!;

    /// <summary>
    /// The address the external system last reported, when it reports one. Refreshed by sync and
    /// used only to seed an automatic match — never to identify the row.
    /// </summary>
    public string? Email { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>Last-seen display name, for the admin mapping UI.</summary>
    public string? DisplayName { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>
    /// Last-seen account handle. Shown when there is no email, which for some connectors is the
    /// normal case rather than the exception.
    /// </summary>
    public string? Handle { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>The employee this identity resolves to, or null when unmapped or ignored.</summary>
    public Guid? EmployeeId { get; private set; }

    /// <summary>The employee this identity resolves to.</summary>
    public Employee? Employee { get; private set; }

    /// <summary>How this mapping was established. Gates whether a sync may revise it.</summary>
    public ExternalIdentityMappingStatus Status { get; private set; }

    /// <summary>
    /// When a sync last saw this identity on a work item. Drives ordering in the review queue so
    /// admins map the people who are actually active first.
    /// </summary>
    public Instant LastSeen { get; private set; }

    /// <summary>True when an admin has decided this row and a sync must leave it alone.</summary>
    public bool IsAdminDecided =>
        Status is ExternalIdentityMappingStatus.ManuallyMapped or ExternalIdentityMappingStatus.Ignored;

    /// <summary>Records an identity a sync encountered but could not resolve to an employee.</summary>
    public static ExternalIdentityMapping CreateUnmapped(
        Connector connector,
        Guid connectionId,
        string externalId,
        string? email,
        string? displayName,
        string? handle,
        Instant lastSeen) =>
        new(connector, connectionId, externalId, email, displayName, handle, null, ExternalIdentityMappingStatus.Unmapped, lastSeen);

    /// <summary>Records an identity a sync resolved by matching its address to an employee.</summary>
    public static ExternalIdentityMapping CreateAutoMatched(
        Connector connector,
        Guid connectionId,
        string externalId,
        string? email,
        string? displayName,
        string? handle,
        Guid employeeId,
        Instant lastSeen) =>
        new(connector, connectionId, externalId, email, displayName, handle,
            Guard.Against.Default(employeeId, nameof(employeeId)),
            ExternalIdentityMappingStatus.AutoMatched, lastSeen);

    /// <summary>
    /// Refreshes what the external system reports about this identity, and re-points the employee
    /// when the sync inferred one.
    /// </summary>
    /// <remarks>
    /// An admin-decided row keeps its employee and status — only the descriptive fields and
    /// <see cref="LastSeen"/> move. That is the whole reason this table exists separately from
    /// <see cref="EmployeeEmail"/>, so the guard lives in the domain where a caller cannot skip it.
    /// </remarks>
    public void RefreshFromSync(
        string? email,
        string? displayName,
        string? handle,
        Guid? autoMatchedEmployeeId,
        Instant lastSeen)
    {
        Email = email;
        DisplayName = displayName;
        Handle = handle;
        LastSeen = lastSeen;

        if (IsAdminDecided)
            return;

        if (autoMatchedEmployeeId.HasValue)
        {
            EmployeeId = autoMatchedEmployeeId;
            Employee = null;
            Status = ExternalIdentityMappingStatus.AutoMatched;
        }
        else
        {
            // The address stopped resolving (the employee left, or the people connector dropped
            // the address). Fall back to unmapped so it re-enters the review queue rather than
            // silently keeping a stale attribution.
            EmployeeId = null;
            Employee = null;
            Status = ExternalIdentityMappingStatus.Unmapped;
        }
    }

    /// <summary>
    /// Re-keys a row the seed migration created from an address onto the external system's real
    /// identity id, the first time a sync reports one for that address.
    /// </summary>
    /// <remarks>
    /// The migration that introduced this table had no identity ids to seed from — work items only
    /// ever stored the resolved employee — so it keyed its rows on the address the old matching
    /// resolved on. This is the one path allowed to change <see cref="ExternalId"/>, and only from
    /// such a placeholder: it refuses when the current key is not this row's own address, so a real
    /// identity id is never silently rewritten by another.
    /// </remarks>
    /// <returns>True when the row was adopted; false when it is not an adoptable placeholder.</returns>
    public bool TryAdoptExternalId(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return false;

        var candidate = externalId.Trim();
        if (string.Equals(ExternalId, candidate, StringComparison.Ordinal))
            return true;

        // A placeholder is a key equal to this row's own address. Anything else is a real identity.
        if (string.IsNullOrWhiteSpace(Email) || !string.Equals(ExternalId, Email, StringComparison.OrdinalIgnoreCase))
            return false;

        ExternalId = candidate;
        return true;
    }

    /// <summary>Points this identity at an employee by admin decision.</summary>
    public Result MapToEmployee(Guid employeeId)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure("An employee is required to map an external identity.");

        EmployeeId = employeeId;
        Employee = null;
        Status = ExternalIdentityMappingStatus.ManuallyMapped;

        return Result.Success();
    }

    /// <summary>
    /// Marks this identity as one that will never have an employee. Survives sync so it stays out
    /// of the review queue.
    /// </summary>
    public void Ignore()
    {
        EmployeeId = null;
        Employee = null;
        Status = ExternalIdentityMappingStatus.Ignored;
    }

    /// <summary>
    /// Clears an admin decision, returning the row to the review queue. The next sync is free to
    /// auto-match it again.
    /// </summary>
    public void ClearDecision()
    {
        EmployeeId = null;
        Employee = null;
        Status = ExternalIdentityMappingStatus.Unmapped;
    }
}
