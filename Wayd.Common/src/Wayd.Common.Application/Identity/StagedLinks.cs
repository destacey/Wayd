namespace Wayd.Common.Application.Identity;

/// <summary>
/// How long a staged link stays open for the sign-in that completes it: an admin-created Entra user's first sign-in,
/// a tenant migration, or a provider migration. Past it the link is ignored and an administrator stages it again, so
/// a link forgotten after a migration cannot be claimed months later.
/// </summary>
public static class StagedLinks
{
    /// <summary>The time from staging a link to its expiry.</summary>
    public static readonly Duration Lifetime = Duration.FromDays(14);

    /// <summary>When a link staged at <paramref name="stagedAt"/> expires; null for a link staged before its time was recorded, which counts as expired.</summary>
    public static Instant? ExpiresAt(Instant? stagedAt) => stagedAt + Lifetime;

    /// <summary>Whether a link staged at <paramref name="stagedAt"/> can still be completed at <paramref name="now"/>.</summary>
    public static bool IsOpen(Instant? stagedAt, Instant now) => stagedAt >= now - Lifetime;
}
