using Microsoft.Extensions.DependencyInjection;
using Wayd.Integrations.Abstractions;

namespace Wayd.Infrastructure.ConnectorModules;

public static class ConnectorModuleExtensions
{
    /// <summary>
    /// Registers every connector module in this assembly. Each module is the self-contained DI
    /// manifest for one connector — adding a connector means adding a module class here, not
    /// editing shared registration code.
    /// </summary>
    public static IServiceCollection AddConnectorModules(this IServiceCollection services)
    {
        foreach (var module in DiscoverModules())
            module.Register(services);

        return services;
    }

    /// <summary>
    /// All connector modules in this assembly, ordered by connector for deterministic
    /// registration. Public so architecture tests can verify the module manifest against the
    /// <c>Connector</c> enum and capability declarations.
    /// </summary>
    public static IReadOnlyList<IConnectorModule> DiscoverModules() =>
        [.. typeof(ConnectorModuleExtensions).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IConnectorModule).IsAssignableFrom(t))
            .Select(t => (IConnectorModule)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Connector)];
}
