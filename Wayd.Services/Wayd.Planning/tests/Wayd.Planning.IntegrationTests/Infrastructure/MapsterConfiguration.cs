using Mapster;
using Mapster.Utils;
using Wayd.Planning.Application.Roadmaps.Dtos;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Registers the application's Mapster mappings exactly as the host does at startup, so handlers that map
/// with <c>.Adapt&lt;&gt;</c> behave here as they do in production. Thread-safe and idempotent.
/// </summary>
internal static class MapsterConfiguration
{
    private static readonly Lazy<bool> _initialized = new(() =>
    {
        var assembly = typeof(RoadmapActivityListDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    /// <summary>Runs the registration once per test process.</summary>
    public static void Ensure() => _ = _initialized.Value;
}
