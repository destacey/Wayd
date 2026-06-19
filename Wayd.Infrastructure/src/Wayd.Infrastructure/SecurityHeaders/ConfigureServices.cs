using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Wayd.Infrastructure.SecurityHeaders;

internal static class ConfigureServices
{
    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration config)
    {
        var settings = config.GetSection(nameof(SecurityHeaderSettings)).Get<SecurityHeaderSettings>();

        if (settings?.Enable is true)
        {
            app.Use(async (context, next) =>
            {
                if (!string.IsNullOrWhiteSpace(settings.XFrameOptions))
                {
                    context.Response.Headers.Append(HeaderNames.XFRAMEOPTIONS, settings.XFrameOptions);
                }

                if (!string.IsNullOrWhiteSpace(settings.XContentTypeOptions))
                {
                    context.Response.Headers.Append(HeaderNames.XCONTENTTYPEOPTIONS, settings.XContentTypeOptions);
                }

                if (!string.IsNullOrWhiteSpace(settings.ReferrerPolicy))
                {
                    context.Response.Headers.Append(HeaderNames.REFERRERPOLICY, settings.ReferrerPolicy);
                }

                if (!string.IsNullOrWhiteSpace(settings.PermissionsPolicy))
                {
                    context.Response.Headers.Append(HeaderNames.PERMISSIONSPOLICY, settings.PermissionsPolicy);
                }

                if (!string.IsNullOrWhiteSpace(settings.SameSite))
                {
                    context.Response.Headers.Append(HeaderNames.SAMESITE, settings.SameSite);
                }

                if (!string.IsNullOrWhiteSpace(settings.XXSSProtection))
                {
                    context.Response.Headers.Append(HeaderNames.XXSSPROTECTION, settings.XXSSProtection);
                }

                await next();
            });
        }

        return app;
    }
}