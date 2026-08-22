namespace Wayd.Common.Application.Identity.Tokens;

/// <summary>
/// One live sign-in shown on the account's Sessions tab. Carries no token or hash — everything
/// here is display detail plus the id needed to revoke the row.
/// </summary>
public sealed record UserSessionResponse(
    Guid Id,
    string? DeviceLabel,
    string? IpAddress,
    Instant CreatedAt,
    Instant LastUsedAt,
    Instant ExpiresAt,
    bool IsCurrent);
