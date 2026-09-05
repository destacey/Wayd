using System.Reflection;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Imports;
using Wayd.Organization.Application.Teams.Imports;

namespace Wayd.Organization.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddOrganizationApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IImportDefinition, TeamMembershipImportDefinition>();

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
