using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Infrastructure.Cors;

internal static class ConfigureServices
{
    private const string CorsPolicy = nameof(CorsPolicy);

    internal static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration config)
    {
        var corsSettings = config.GetSection(nameof(CorsSettings)).Get<CorsSettings>();
        var origins = new List<string>();

        if (corsSettings?.WebClient is not null)
            origins.AddRange(corsSettings.WebClient.Split(';', StringSplitOptions.RemoveEmptyEntries));

        return services.AddCors(opt =>
            opt.AddPolicy(CorsPolicy, policy =>
                policy.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    // Not a header browsers expose by default; the client reads it to wait out a rate limit.
                    .WithExposedHeaders(Microsoft.Net.Http.Headers.HeaderNames.RetryAfter)
                    .WithOrigins(origins.ToArray())
                    .SetIsOriginAllowedToAllowWildcardSubdomains())
        );

    }

    internal static IApplicationBuilder UseCorsPolicy(this IApplicationBuilder app) =>
        app.UseCors(CorsPolicy);
}