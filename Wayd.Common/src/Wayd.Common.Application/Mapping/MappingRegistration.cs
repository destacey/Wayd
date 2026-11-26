using System.Reflection;
using Mapster;

namespace Wayd.Common.Application.Mapping;

/// <summary>Registers an assembly's Mapster mappings on the global configuration, once per process.</summary>
/// <remarks>
/// <see cref="TypeAdapterConfig.GlobalSettings"/> is one per process and its rules are not safe to change while
/// another thread maps. Each host's start-up registers its modules' mappings, and a test process runs several hosts
/// side by side, so a second host re-registering a rule could race the first one mapping a request. Once every
/// assembly is registered, nothing writes to the configuration again.
/// </remarks>
public static class MappingRegistration
{
    private static readonly Lock Gate = new();
    private static readonly HashSet<Assembly> Registered = [];

    /// <summary>
    /// Runs <paramref name="configure"/> against the global configuration the first time <paramref name="assembly"/>
    /// is registered in this process; later calls for the same assembly do nothing.
    /// </summary>
    /// <param name="assembly">The assembly whose mappings <paramref name="configure"/> registers.</param>
    /// <param name="configure">Registers the assembly's mappings and any global settings it needs.</param>
    public static void Once(Assembly assembly, Action<TypeAdapterConfig> configure)
    {
        lock (Gate)
        {
            if (Registered.Contains(assembly))
                return;

            configure(TypeAdapterConfig.GlobalSettings);
            Registered.Add(assembly);
        }
    }
}
