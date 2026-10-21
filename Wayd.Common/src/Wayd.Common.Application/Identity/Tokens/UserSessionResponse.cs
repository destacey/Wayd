namespace Wayd.Common.Application.Identity.Tokens;

/// <summary>
/// One live sign-in shown on the account's Sessions tab. Carries no token or hash — everything
/// here is display detail plus the id needed to revoke the row.
/// </summary>
/// <param name="Id">The session's id, which revokes it.</param>
/// <param name="DeviceLabel">The browser and platform it was opened from, or for an application connected with OAuth, the application's name for itself.</param>
/// <param name="IpAddress">The address it was opened from.</param>
/// <param name="CreatedAt">When it was opened.</param>
/// <param name="LastUsedAt">When it was last refreshed.</param>
/// <param name="ExpiresAt">When it ends unless refreshed.</param>
/// <param name="IsCurrent">Whether it is the caller's own session.</param>
/// <param name="ClientId">The URL identifying the application connected with OAuth, or null for a sign-in to Wayd itself.</param>
public sealed record UserSessionResponse(
    Guid Id,
    string? DeviceLabel,
    string? IpAddress,
    Instant CreatedAt,
    Instant LastUsedAt,
    Instant ExpiresAt,
    bool IsCurrent,
    string? ClientId);
