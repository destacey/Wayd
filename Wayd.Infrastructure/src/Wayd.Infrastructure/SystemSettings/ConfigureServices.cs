using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.SystemSettings;

namespace Wayd.Infrastructure.SystemSettings;

internal static class ConfigureServices
{
    public static IServiceCollection AddSystemSettings(this IServiceCollection services)
    {
        services.AddScoped<ISystemSettingsStore, SystemSettingsStore>();

        return services;
    }
}
