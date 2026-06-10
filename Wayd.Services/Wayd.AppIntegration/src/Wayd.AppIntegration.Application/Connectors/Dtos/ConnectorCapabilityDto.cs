using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connectors.Dtos;

/// <summary>
/// Wire projection of a <see cref="ConnectorCapability"/>: the capability plus the display
/// category (the enum's Display GroupName) the UI groups it under.
/// </summary>
public sealed record ConnectorCapabilityDto
{
    public int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The display category grouping this capability (e.g. "Work Management").</summary>
    public required string Category { get; init; }

    public static ConnectorCapabilityDto FromEnum(ConnectorCapability capability) => new()
    {
        Id = (int)capability,
        Name = capability.GetDisplayName(),
        Category = capability.GetDisplayGroupName() ?? "Other",
    };
}
