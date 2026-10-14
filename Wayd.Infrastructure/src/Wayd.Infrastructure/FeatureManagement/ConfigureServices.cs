using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Wayd.Common.Application.FeatureManagement;

namespace Wayd.Infrastructure.FeatureManagement;

internal static class ConfigureServices
{
    public static IServiceCollection AddWaydFeatureManagement(this IServiceCollection services)
    {
        services.AddSingleton<DatabaseFeatureDefinitionProvider>();
        services.AddSingleton<IFeatureDefinitionProvider>(sp =>
            sp.GetRequiredService<DatabaseFeatureDefinitionProvider>());
        services.AddSingleton<IFeatureFlagCacheInvalidator>(sp =>
            sp.GetRequiredService<DatabaseFeatureDefinitionProvider>());

        services.AddFeatureManagement();
        services.AddScoped<IFeatureFlags, FeatureFlagReader>();

        return services;
    }
}
