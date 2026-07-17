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
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            //config.AddOpenBehavior(typeof(UnhandledExceptionBehavior<,>));  // TODO: currently relying on ExceptionMiddleware, do we need more granular control?
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });

        services.AddScoped<IDispatcher, MediatRDispatcher>();

        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);

        return services;
    }
}
