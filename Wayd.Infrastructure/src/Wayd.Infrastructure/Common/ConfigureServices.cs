using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Infrastructure.Common.Services;

namespace Wayd.Infrastructure.Common;

internal static class ConfigureServices
{
    internal static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddScoped<ITokenHashingService, TokenHashingService>()
            .AddServices(typeof(ITransientService), ServiceLifetime.Transient)
            .AddServices(typeof(IScopedService), ServiceLifetime.Scoped);

    internal static IServiceCollection AddServices(this IServiceCollection services, Type interfaceType, ServiceLifetime lifetime)
    {
        var interfaceTypes =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => IsApplicationAssembly(a.GetName()))
                .SelectMany(s => s.GetTypes())
                .Where(t => interfaceType.IsAssignableFrom(t)
                            && t.IsClass && !t.IsAbstract)
                .Select(t => new
                {
                    Service = t.GetInterfaces().FirstOrDefault(),
                    Implementation = t
                })
                .Where(t => t.Service is not null
                            && interfaceType.IsAssignableFrom(t.Service));

        foreach (var type in interfaceTypes)
        {
            services.AddService(type.Service!, type.Implementation, lifetime);
        }

        return services;
    }

    /// <summary>Whether the service scan may register types from this assembly.</summary>
    /// <remarks>
    /// The scan covers whatever the process has loaded, which in a test host includes test libraries. A test
    /// double implementing a marker interface is registered after the real service and replaces it, so a
    /// constructible one would go unnoticed. Test assemblies are recognised by name: a segment that starts
    /// with <c>Test</c> (<c>Wayd.Tests.Shared</c>, <c>Wayd.TestData.Core</c>) or ends with <c>Tests</c>.
    /// </remarks>
    internal static bool IsApplicationAssembly(AssemblyName assembly) =>
        assembly.Name is { } name
        && name.StartsWith("Wayd.", StringComparison.Ordinal)
        && !name.Split('.').Any(segment =>
            segment.StartsWith("Test", StringComparison.Ordinal) || segment.EndsWith("Tests", StringComparison.Ordinal));

    internal static IServiceCollection AddService(this IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime) =>
        lifetime switch
        {
            ServiceLifetime.Transient => services.AddTransient(serviceType, implementationType),
            ServiceLifetime.Scoped => services.AddScoped(serviceType, implementationType),
            ServiceLifetime.Singleton => services.AddSingleton(serviceType, implementationType),
            _ => throw new ArgumentException("Invalid lifeTime", nameof(lifetime))
        };
}