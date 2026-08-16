namespace Wayd.Infrastructure.SecurityHeaders;

/// <summary>
/// Bound from <c>Configurations/securityheaders.json</c>. Property names must match the JSON keys
/// exactly — the configuration binder does not normalise hyphens, so a key like <c>XXSS-Protection</c>
/// silently binds null against a property named <c>XXSSProtection</c>.
/// <para>
/// Only values with genuine per-deployment variance live here. The security invariants
/// (X-Content-Type-Options, X-Frame-Options and the CSP) are constants in
/// <see cref="SecurityHeaderDefaults"/> — a header that has exactly one correct value cannot be
/// misconfigured if it cannot be configured.
/// </para>
/// </summary>
public class SecurityHeaderSettings
{
    public bool Enable { get; set; } = true;

    /// <summary>
    /// Referrer-Policy. Falls back to <see cref="SecurityHeaderDefaults.ReferrerPolicy"/> when unset,
    /// so a missing or misspelled key degrades to the secure value rather than to no header.
    /// </summary>
    public string? ReferrerPolicy { get; set; }

    /// <summary>
    /// Permissions-Policy. Falls back to <see cref="SecurityHeaderDefaults.PermissionsPolicy"/> when
    /// unset. Configurable because a future feature may legitimately need one of these APIs.
    /// </summary>
    public string? PermissionsPolicy { get; set; }

    /// <summary>
    /// Strict-Transport-Security. Off by default: TLS terminates upstream, so the edge is the correct
    /// place to emit HSTS. Enabling this on a host that is not fully HTTPS-ready locks clients out for
    /// the max-age duration.
    /// </summary>
    public bool EnableHsts { get; set; }

    public int HstsMaxAgeSeconds { get; set; } = SecurityHeaderDefaults.HstsMaxAgeSeconds;

    public bool HstsIncludeSubDomains { get; set; } = true;
}
