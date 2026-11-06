using Mapster;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Application.FeatureManagement.Dtos;

/// <summary>
/// A feature flag as the Feature Flags page shows it. Everything but <see cref="IsEnabled"/> comes from the
/// flag's definition in code.
/// </summary>
public sealed record FeatureFlagDto : IMapFrom<FeatureFlag>
{
    /// <summary>The flag's id, used to toggle it.</summary>
    public int Id { get; set; }

    /// <summary>The kebab-case key the code gates on.</summary>
    public string Name { get; set; } = default!;

    /// <summary>The flag's display name.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>What turning the flag on makes available.</summary>
    public string? Description { get; set; }

    /// <summary>The area of the app the flag belongs to.</summary>
    public FeatureFlagModule Module { get; set; }

    /// <summary>
    /// Whether the flag turns its whole module on or off. While it is off, the module's other flags have no effect.
    /// </summary>
    public bool IsModuleSwitch { get; set; }

    /// <summary>Whether the feature is on for the whole organization.</summary>
    public bool IsEnabled { get; set; }
}
