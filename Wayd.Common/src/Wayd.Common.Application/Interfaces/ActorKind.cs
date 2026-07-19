namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Classifies who the current caller is (see <see cref="ICurrentUser.Kind"/>): a real signed-in or
/// impersonated user, a genuinely anonymous HTTP caller, or the platform itself acting with no user
/// in the picture (scheduled jobs, durable message delivery, startup work). The distinction drives
/// <c>ICurrentPrincipal</c> — system grants all permissions, anonymous denies all — and audit
/// attribution, where system writes are stamped with <c>SystemIdentity.UserId</c> instead of being
/// left empty.
/// </summary>
public enum ActorKind
{
    /// <summary>An HTTP request with no authenticated user. Denied all permissions.</summary>
    Anonymous,

    /// <summary>A signed-in user, or a background scope acting on a specific user's behalf.</summary>
    User,

    /// <summary>
    /// The platform acting on its own behalf — no HTTP request and no acting user (scheduled jobs,
    /// durable message delivery, startup). Granted all permissions.
    /// </summary>
    System,
}
