namespace Wayd.Infrastructure.SecurityHeaders;

/// <summary>
/// Header names with no constant in <see cref="Microsoft.Net.Http.Headers.HeaderNames"/>. Use the
/// framework's constants for everything else — a hand-typed name is not validated anywhere, and a
/// typo makes the header silently useless rather than failing the build.
/// </summary>
internal static class HeaderNames
{
    internal const string ReferrerPolicy = "Referrer-Policy";
    internal const string PermissionsPolicy = "Permissions-Policy";
}
