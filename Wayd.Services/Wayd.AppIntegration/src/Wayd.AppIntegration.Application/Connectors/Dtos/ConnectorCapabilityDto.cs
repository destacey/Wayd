using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connectors.Dtos;

/// <summary>
/// A capability a connector supports, with the display category it is grouped under.
/// </summary>
/// <remarks>
/// Wire projection of a <see cref="ConnectorCapability"/>; the category is the enum's Display GroupName.
/// </remarks>
public sealed record ConnectorCapabilityDto
{
    public int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The display category grouping this capability (e.g. "Work Management"); "Other" when the capability has no category.</summary>
    public required string Category { get; init; }

    public static ConnectorCapabilityDto FromEnum(ConnectorCapability capability) => new()
    {
        Id = (int)capability,
        Name = capability.GetDisplayName(),
        Category = capability.GetDisplayGroupName() ?? "Other",
    };
}
