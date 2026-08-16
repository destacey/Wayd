using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NetHeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace Wayd.Infrastructure.SecurityHeaders;

internal static class ConfigureServices
{
    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration config)
    {
        // A missing section must not mean "no headers" — bind to defaults instead. Only an explicit
        // Enable=false turns the middleware off.
        var settings = config.GetSection(nameof(SecurityHeaderSettings)).Get<SecurityHeaderSettings>()
            ?? new SecurityHeaderSettings();

        if (!settings.Enable)
        {
            return app;
        }

        // Resolved once at startup: these never vary per request, and an unset value falls back to the
        // secure constant rather than to no header at all.
        var referrerPolicy = Coalesce(settings.ReferrerPolicy, SecurityHeaderDefaults.ReferrerPolicy);
        var permissionsPolicy = Coalesce(settings.PermissionsPolicy, SecurityHeaderDefaults.PermissionsPolicy);
        var hsts = BuildHstsValue(settings);

        app.Use(async (context, next) =>
        {
            // Assign rather than Append: Append would duplicate the value if an upstream proxy already
            // set the header, and a duplicated X-Frame-Options is ignored by some browsers.
            var headers = context.Response.Headers;

            headers[NetHeaderNames.XContentTypeOptions] = SecurityHeaderDefaults.XContentTypeOptions;
            headers[NetHeaderNames.XFrameOptions] = SecurityHeaderDefaults.XFrameOptions;
            headers[NetHeaderNames.ContentSecurityPolicy] = SecurityHeaderDefaults.ContentSecurityPolicy;
            headers[HeaderNames.ReferrerPolicy] = referrerPolicy;
            headers[HeaderNames.PermissionsPolicy] = permissionsPolicy;

            // Only meaningful over TLS, and ignored by browsers on a plain-HTTP response.
            if (hsts is not null && context.Request.IsHttps)
            {
                headers[NetHeaderNames.StrictTransportSecurity] = hsts;
            }

            await next();
        });

        return app;
    }

    private static string Coalesce(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;

    private static string? BuildHstsValue(SecurityHeaderSettings settings)
    {
        if (!settings.EnableHsts || settings.HstsMaxAgeSeconds <= 0)
        {
            return null;
        }

        return settings.HstsIncludeSubDomains
            ? $"max-age={settings.HstsMaxAgeSeconds}; includeSubDomains"
            : $"max-age={settings.HstsMaxAgeSeconds}";
    }
}
