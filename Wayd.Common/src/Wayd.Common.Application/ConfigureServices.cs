using System.Reflection;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Dispatching;

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

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
