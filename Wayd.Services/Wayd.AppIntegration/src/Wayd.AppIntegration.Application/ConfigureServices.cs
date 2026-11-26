using System.Reflection;
using FluentValidation;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Mapping;

namespace Wayd.AppIntegration.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddAppIntegrationApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        MappingRegistration.Once(assembly, mapping =>
        {
            mapping.Scan(assembly);
            mapping.ScanInheritedTypes(assembly);
            mapping.AllowImplicitSourceInheritance = true;
            mapping.AllowImplicitDestinationInheritance = true;
        });

        return services;
    }
}
