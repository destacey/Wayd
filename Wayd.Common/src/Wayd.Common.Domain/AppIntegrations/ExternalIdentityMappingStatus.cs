namespace Wayd.Common.Domain.AppIntegrations;

/// <summary>
/// How an external system's user came to point at (or deliberately not point at) a Wayd employee.
/// The distinction matters because a sync may revise what it inferred but must never overwrite
/// what an admin decided.
/// </summary>
public enum ExternalIdentityMappingStatus
{
    /// <summary>Seen on synced work, but no employee could be resolved. Awaits an admin decision.</summary>
    Unmapped = 0,

    /// <summary>
    /// Resolved automatically by matching the external address against an employee's known work
    /// addresses. A later sync may revise this if the address moves to a different employee.
    /// </summary>
    AutoMatched = 1,

    /// <summary>Set by an admin. Never revised by a sync.</summary>
    ManuallyMapped = 2,

    /// <summary>
    /// An admin decided this identity has no employee and never should — build service accounts,
    /// bots, and AAD groups. Never revised by a sync, and never re-surfaced for review; without
    /// it the unmapped queue never reaches zero and admins stop reading it.
    /// </summary>
    Ignored = 3,
}
