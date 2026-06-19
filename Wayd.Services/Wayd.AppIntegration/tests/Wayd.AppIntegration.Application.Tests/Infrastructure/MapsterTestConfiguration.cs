using Mapster;
using Mapster.Utils;
using Wayd.AppIntegration.Application.Connections.Dtos;

namespace Wayd.AppIntegration.Application.Tests.Infrastructure;

/// <summary>
/// Registers the application's Mapster mappings exactly as AddAppIntegrationApplication does at
/// startup, so handler tests that exercise .Adapt&lt;&gt; see the same Include-based polymorphic
/// configuration. Thread-safe and idempotent — call from any test class constructor that needs it.
/// </summary>
public static class MapsterTestConfiguration
{
    private static readonly Lazy<bool> _initialized = new(() =>
    {
        var assembly = typeof(ConnectionListDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        TypeAdapterConfig.GlobalSettings.AllowImplicitSourceInheritance = true;
        TypeAdapterConfig.GlobalSettings.AllowImplicitDestinationInheritance = true;
        return true;
    });

    public static void Ensure() => _ = _initialized.Value;
}
