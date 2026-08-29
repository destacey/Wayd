using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

/// <summary>
/// Identifies who is performing a managed PPM action and whether they hold the domain-wide
/// PPM administrator grant (<c>Permissions.ProjectPortfolioManagement.Administer</c>).
///
/// The administrator grant substitutes for <em>role membership</em> only — it never substitutes for
/// the resource permission itself. A caller still needs e.g. <c>Permissions.Projects.Update</c> to
/// reach a project endpoint; this type only answers "do they count as leadership on this record".
///
/// Construct once per request in the application layer (from <c>ICurrentPrincipal</c>) and pass it
/// into the aggregate. The domain never resolves claims itself.
/// </summary>
/// <param name="EmployeeId">The acting employee. Every managed action requires a linked employee.</param>
/// <param name="IsPpmAdministrator">True when the actor holds the domain-wide PPM administrator grant.</param>
/// <param name="UserId">
/// The acting user account. Carried alongside <paramref name="EmployeeId"/> because the two are
/// separately mutable: a user can be unlinked from or relinked to an employee after the fact, so an
/// action attributed only by employee cannot be traced back to the account that performed it. Records
/// that freeze attribution at write time (such as project status history) store both.
/// </param>
public sealed record PpmActor(Guid EmployeeId, bool IsPpmAdministrator, string UserId)
{
    /// <summary>
    /// A system actor — background jobs, importers, and replication paths that run without a
    /// signed-in user. Bypasses membership for the same reason the administrator grant does, but is
    /// deliberately a distinct construction so an accidental default can never look like a real user.
    /// Attributed to <see cref="SystemUser.Id"/> and to no employee.
    /// </summary>
    public static PpmActor System { get; } = new(Guid.Empty, IsPpmAdministrator: true, SystemUser.Id);

    /// <summary>
    /// The acting employee, or null when this is the <see cref="System"/> actor, which has no employee.
    /// </summary>
    public Guid? EmployeeIdOrNull => EmployeeId == Guid.Empty ? null : EmployeeId;

    /// <summary>
    /// The event-envelope attribution for this actor, for stamping domain events raised by the action it
    /// authorizes.
    /// </summary>
    /// <remarks>
    /// Derived rather than passed separately so the two can never disagree: the actor that authorized a
    /// change is by definition the actor that caused its events. <see cref="System"/> maps to
    /// <see cref="EventActor.System"/>, so the importers and replication paths that already pass it (grep
    /// <c>PpmActor.System</c>) attribute their events to the platform without any further change.
    /// </remarks>
    public EventActor ToEventActor() =>
        string.Equals(UserId, SystemUser.Id, StringComparison.OrdinalIgnoreCase)
            ? EventActor.System
            : EventActor.User(UserId);
}
