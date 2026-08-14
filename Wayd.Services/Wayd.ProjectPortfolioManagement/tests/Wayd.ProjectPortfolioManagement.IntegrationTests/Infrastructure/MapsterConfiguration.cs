using Mapster;
using Mapster.Utils;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Registers the application's Mapster mappings exactly as the host does at startup, so handlers that map
/// with <c>.Adapt&lt;&gt;</c> behave here as they do in production. Thread-safe and idempotent.
/// </summary>
internal static class MapsterConfiguration
{
    private static readonly Lazy<bool> _initialized = new(() =>
    {
        var assembly = typeof(ProjectStatusHistoryDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    public static void Ensure() => _ = _initialized.Value;
}
