using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.Planning.Application.PlanningIntervals.Imports;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.Risks.Imports;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Common.Application.Mapping;

namespace Wayd.Planning.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddPlanningApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, PlanningIntervalImportDefinition>();
        services.AddScoped<IImportDefinition, PlanningIntervalObjectiveImportDefinition>();
        services.AddScoped<IImportDefinition, RiskImportDefinition>();

        services.AddScoped<IPokerSessionAccess, PokerSessionAccess>();
        services.AddScoped<IStoryMapAccess, StoryMapAccess>();

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
