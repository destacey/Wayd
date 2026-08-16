namespace Wayd.Infrastructure.SecurityHeaders;

/// <summary>
/// Fixed security-header values. These are invariants rather than deployment settings: each has one
/// correct value, so making them configurable only creates a way to get them wrong — which is how
/// every header in this middleware came to be silently absent.
/// </summary>
internal static class SecurityHeaderDefaults
{
    /// <summary>The only valid value for this header.</summary>
    internal const string XContentTypeOptions = "nosniff";

    /// <summary>
    /// Legacy mirror of the CSP's <c>frame-ancestors 'none'</c>, for browsers predating CSP Level 2.
    /// Kept in step with <see cref="ContentSecurityPolicy"/> deliberately — the two disagreeing is
    /// worse than either alone.
    /// </summary>
    internal const string XFrameOptions = "DENY";

    /// <summary>
    /// Omits <c>default-src</c>/<c>script-src</c>: this host also serves Swagger UI, ReDoc and the
    /// Hangfire dashboard, which all rely on inline scripts. Every directive here is script-agnostic
    /// and safe for every response the API emits.
    /// </summary>
    internal const string ContentSecurityPolicy =
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    internal const string ReferrerPolicy = "strict-origin-when-cross-origin";

    internal const string PermissionsPolicy = "geolocation=(), camera=(), microphone=()";

    /// <summary>Two years, per OWASP. Only emitted when HSTS is explicitly enabled.</summary>
    internal const int HstsMaxAgeSeconds = 63072000;
}
