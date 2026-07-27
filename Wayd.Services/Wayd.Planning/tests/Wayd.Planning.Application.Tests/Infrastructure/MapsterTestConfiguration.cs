using Mapster;
using Mapster.Utils;
using Wayd.Planning.Application.StoryMaps.Dtos;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// Provides a SCOPED Mapster config that registers the Planning application's IMapFrom mappings the
/// same way AddPlanningApplication does at startup — including custom ConfigureMapping overrides such
/// as the task checklist counts and the status display-name.
///
/// This deliberately does NOT mutate <c>TypeAdapterConfig.GlobalSettings</c>: doing so leaks
/// PreserveReference / implicit-inheritance flags into every other test in the run and changes how
/// their <c>ProjectToType</c> behaves. Mapping tests must adapt with this scoped config, e.g.
/// <c>source.Adapt&lt;TDto&gt;(MapsterTestConfiguration.Config)</c>.
/// </summary>
public static class MapsterTestConfiguration
{
    private static readonly Lazy<TypeAdapterConfig> _config = new(() =>
    {
        var config = new TypeAdapterConfig();
        var assembly = typeof(StoryMapDetailsDto).Assembly;
        config.Scan(assembly);
        config.ScanInheritedTypes(assembly);
        config.Default.PreserveReference(true);
        config.AllowImplicitSourceInheritance = true;
        config.AllowImplicitDestinationInheritance = true;
        return config;
    });

    /// <summary>The scoped config to pass to <c>.Adapt&lt;T&gt;(config)</c> in mapping tests.</summary>
    public static TypeAdapterConfig Config => _config.Value;
}
