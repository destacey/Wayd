using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wayd.Infrastructure.RateLimiting;

/// <summary>
/// Registers the API's rate-limiting policies. An endpoint is limited only when it names a policy, with
/// <c>RequireRateLimiting</c> on a minimal endpoint or <c>[EnableRateLimiting]</c> on a controller action.
/// </summary>
internal static class ConfigureServices
{
    /// <summary>
    /// Registers the policies in <see cref="RateLimitPolicies"/> and their settings, which must give every limit a
    /// positive permit count and window or the host does not start.
    /// </summary>
    internal static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RateLimitSettings>()
            .Bind(config.GetSection(RateLimitSettings.SectionName))
            .Validate(
                s => s.All().All(l => l.PermitLimit > 0 && l.WindowSeconds > 0),
                $"Every limit in '{RateLimitSettings.SectionName}' needs a PermitLimit and WindowSeconds above zero.")
            .ValidateOnStart();

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(RateLimitPolicies.OAuthAuthorize, context => PerClientAddress(context, s => s.OAuthAuthorize));
            options.AddPolicy(RateLimitPolicies.OAuthToken, context => PerClientAddress(context, s => s.OAuthToken));
            options.AddPolicy(RateLimitPolicies.AuthSignIn, context => PerClientAddress(context, s => s.AuthSignIn));
            options.AddPolicy(RateLimitPolicies.AuthRefresh, context => PerClientAddress(context, s => s.AuthRefresh));
            options.AddPolicy(RateLimitPolicies.AuthProviders, context => PerClientAddress(context, s => s.AuthProviders));
            options.AddPolicy(RateLimitPolicies.AuthSetup, context => PerClientAddress(context, s => s.AuthSetup));
        });
    }

    /// <summary>Applies the policies to the endpoints that name them; it must run after routing has chosen the endpoint.</summary>
    internal static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app) => app.UseRateLimiter();

    /// <summary>
    /// A fixed window per caller address. Behind a proxy the address is the proxy's, so every caller shares one
    /// window, unless forwarded headers are enabled (<c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c>).
    /// </summary>
    /// <remarks>
    /// The limit is part of the partition key because a limiter is created once per key: keyed on the address alone,
    /// a busy caller would keep the old limit after the settings change.
    /// </remarks>
    private static RateLimitPartition<string> PerClientAddress(HttpContext context, Func<RateLimitSettings, FixedWindowLimit> select)
    {
        var limit = select(context.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitSettings>>().CurrentValue);
        var address = ClientAddressKey.For(context.Connection.RemoteIpAddress);

        return RateLimitPartition.GetFixedWindowLimiter($"{address}|{limit.PermitLimit}|{limit.WindowSeconds}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit.PermitLimit,
            Window = TimeSpan.FromSeconds(limit.WindowSeconds),
            QueueLimit = 0,
        });
    }
}
