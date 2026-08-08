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
public sealed record PpmActor(Guid EmployeeId, bool IsPpmAdministrator)
{
    /// <summary>
    /// A system actor — background jobs, importers, and replication paths that run without a
    /// signed-in user. Bypasses membership for the same reason the administrator grant does, but is
    /// deliberately a distinct construction so an accidental default can never look like a real user.
    /// </summary>
    public static PpmActor System { get; } = new(Guid.Empty, IsPpmAdministrator: true);
}
