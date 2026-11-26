using System.Reflection;
using FluentValidation;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Mapping;

namespace Wayd.Links;

public static class ConfigureServices
{
    public static IServiceCollection AddLinksApplication(this IServiceCollection services)
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
