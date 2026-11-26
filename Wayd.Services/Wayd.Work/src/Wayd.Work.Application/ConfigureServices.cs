using System.Reflection;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Mapping;

namespace Wayd.Work.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddWorkApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        MappingRegistration.Once(assembly, mapping =>
        {
            mapping.Scan(assembly);
            mapping.ScanInheritedTypes(assembly);
        });

        return services;
    }
}
