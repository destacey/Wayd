using System.Reflection;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.DemandManagement.Application.IntakeRequests.Imports;

namespace Wayd.DemandManagement.Application;

/// <summary>
/// Registers the Demand Management module's application services.
/// </summary>
public static class ConfigureServices
{
    /// <summary>
    /// Registers the module's validators and Mapster mappings. Its handlers are discovered by Wolverine
    /// through this type, which <c>WolverineConfiguration</c> lists as a handler assembly marker.
    /// </summary>
    public static IServiceCollection AddDemandManagementApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, IntakeRequestImportDefinition>();

        TypeAdapterConfig.GlobalSettings.Scan(assembly);

        return services;
    }
}
