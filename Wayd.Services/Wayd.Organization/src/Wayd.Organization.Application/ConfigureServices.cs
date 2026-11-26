using System.Reflection;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Common.Application.Mapping;

namespace Wayd.Organization.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddOrganizationApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, TeamImportDefinition>();
        services.AddScoped<IImportDefinition, TeamMemberImportDefinition>();
        services.AddScoped<IImportDefinition, TeamMembershipImportDefinition>();

        MappingRegistration.Once(assembly, mapping =>
        {
            mapping.Scan(assembly);
            mapping.ScanInheritedTypes(assembly);
        });

        return services;
    }
}
