using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.StrategicManagement.Application.StrategicThemes.Imports;
using Wayd.Common.Application.Mapping;

namespace Wayd.StrategicManagement.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddStrategicManagementApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, StrategicThemeImportDefinition>();

        ConfigureMapster(assembly);

        return services;
    }
    private static void ConfigureMapster(Assembly assembly)
    {
        // Global Mapster settings
        MappingRegistration.Once(assembly, mapping =>
        {
            mapping.Scan(assembly);
            mapping.ScanInheritedTypes(assembly);
            mapping.Default.PreserveReference(true);
            mapping.AllowImplicitSourceInheritance = true;
            mapping.AllowImplicitDestinationInheritance = true;
        });
    }
}
