using Mapster;
using Mapster.Utils;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;

namespace Wayd.DemandManagement.Application.Tests.Infrastructure;

/// <summary>
/// Registers the Mapster mappings the host registers at startup for the shared application layer, so a handler
/// that adapts a scoring model sees the same configuration it runs with. Idempotent and thread-safe.
/// </summary>
public static class MapsterTestConfiguration
{
    private static readonly Lazy<bool> Initialized = new(() =>
    {
        var assembly = typeof(ScoringModelDetailsDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    public static void Ensure() => _ = Initialized.Value;
}
