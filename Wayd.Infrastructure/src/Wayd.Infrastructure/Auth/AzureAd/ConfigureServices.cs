using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Serilog;

namespace Wayd.Infrastructure.Auth.AzureAd;

internal static class ConfigureServices
{
    internal static IServiceCollection AddAzureAdAuth(this IServiceCollection services, IConfiguration config)
    {
        var logger = Log.ForContext(typeof(AzureAdJwtBearerEvents));

        // Bind AzureAdSettings so other components (e.g. the OidcProvider seeder)
        // can resolve ClientId via IOptions<AzureAdSettings> instead of parsing
        // IConfiguration directly. The existing GetConfig(IConfiguration) static
        // continues to work because both paths read the same section.
        services.Configure<AzureAdSettings>(config.GetSection(AzureAdSettings.SectionName));

        services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                authentication.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
             .AddMicrosoftIdentityWebApi(
                 jwtOptions => jwtOptions.Events = new AzureAdJwtBearerEvents(logger, config),
                 msIdentityOptions => config.GetSection(AzureAdSettings.SectionName).Bind(msIdentityOptions))
            .EnableTokenAcquisitionToCallDownstreamApi(clientAppOptions => config.GetSection(AzureAdSettings.SectionName).Bind(clientAppOptions))
            .AddInMemoryTokenCaches();

        return services;
    }
}
