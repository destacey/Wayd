using Mapster;
using Mapster.Utils;
using Wayd.Work.Application.WorkTeams.Dtos;

namespace Wayd.Work.Application.Tests.Infrastructure;

/// <summary>
/// Registers the application's Mapster mappings exactly as the host does at startup, so handler tests
/// that project through <c>IMapFrom</c> configurations see the same registration the running
/// application uses. Thread-safe and idempotent — call from any test class constructor that needs it.
/// </summary>
public static class MapsterTestConfiguration
{
    private static readonly Lazy<bool> _initialized = new(() =>
    {
        var assembly = typeof(TeamBacklogHealthDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    public static void Ensure() => _ = _initialized.Value;
}
