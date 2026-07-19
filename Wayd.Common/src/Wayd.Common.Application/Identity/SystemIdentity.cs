namespace Wayd.Common.Application.Identity;

/// <summary>
/// The well-known identity used for system-initiated work that has no human actor — most notably
/// background jobs enqueued without an HTTP context. Surfaced through <c>ICurrentUser.GetUserId()</c>
/// so application code can recognise a trusted system context (e.g. to allow a scheduled maintenance
/// operation to bypass a per-actor authorization check).
/// </summary>
public static class SystemIdentity
{
    /// <summary>
    /// The well-known system user id stamped onto background jobs, audit columns, and message
    /// envelopes. <c>CurrentUser</c> reports it for every <c>ActorKind.System</c> scope (no HTTP
    /// request, no acting user), and <c>HangfireService.EnqueueSystem</c> stamps it explicitly.
    /// </summary>
    public const string UserId = "11111111-1111-1111-1111-111111111111";

    /// <summary>Display name for the system actor.</summary>
    public const string Name = "System";

    /// <summary>Whether the supplied user id is the system identity.</summary>
    public static bool IsSystem(string? userId) =>
        string.Equals(userId, UserId, StringComparison.OrdinalIgnoreCase);
}
