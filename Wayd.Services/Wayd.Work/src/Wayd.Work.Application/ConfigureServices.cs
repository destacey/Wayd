using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Work.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddWorkApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
