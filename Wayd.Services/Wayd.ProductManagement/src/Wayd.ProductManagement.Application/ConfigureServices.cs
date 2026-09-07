using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.ProductManagement.Application.Products.Imports;
using Wayd.ProductManagement.Application.ReleasePackages.Imports;
using Wayd.ProductManagement.Application.Releases.Imports;
using Wayd.ProductManagement.Application.Versions.Imports;

namespace Wayd.ProductManagement.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddProductManagementApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, ProductImportDefinition>();
        services.AddScoped<IImportDefinition, VersionImportDefinition>();
        services.AddScoped<IImportDefinition, ReleaseImportDefinition>();
        services.AddScoped<IImportDefinition, ReleasePackageImportDefinition>();

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
