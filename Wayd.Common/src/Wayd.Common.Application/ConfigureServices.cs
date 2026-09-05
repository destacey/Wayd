using System.Reflection;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Dispatching;
using Wayd.Common.Application.Employees.Imports;
using Wayd.Common.Application.Imports;

namespace Wayd.Common.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddCommonApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        // Dispatch goes through IDispatcher; the Wolverine message bus itself is registered by
        // UseWolverine on the host builder (see Program.cs / AddWaydWolverine).
        services.AddScoped<IDispatcher, WolverineDispatcher>();

        // Import definitions are registered explicitly rather than through the IScopedService marker scan:
        // that scan binds each implementation to its FIRST interface, which is unreliable when several
        // classes share one. Registering them by hand also keeps IEnumerable<IImportDefinition> — what the
        // registry takes instead of a service provider, so Wolverine's codegen can inline it.
        services.AddScoped<IImportPayloadSerializer, ImportPayloadSerializer>();
        services.AddScoped<IImportDefinition, EmployeeImportDefinition>();
        services.AddScoped<IImportDefinitionRegistry, ImportDefinitionRegistry>();

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
