using System.Reflection;
using FluentValidation;
using Mapster;
using Mapster.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Wayd.Links;

public static class ConfigureServices
{
    public static IServiceCollection AddLinksApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
