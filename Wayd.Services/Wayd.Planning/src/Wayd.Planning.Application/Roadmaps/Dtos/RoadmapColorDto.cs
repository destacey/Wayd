using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Dtos;

public sealed record RoadmapColorDto : IMapFrom<RoadmapColor>
{
    /// <summary>
    /// The color, as a hex code (e.g. "#4096FF").
    /// </summary>
    public required string Color { get; set; }

    /// <summary>
    /// The caption describing what the color represents on this Roadmap.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The order of the color within the Roadmap's configured colors.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Whether this is the default color applied to activities that have no color of their own.
    /// </summary>
    public bool IsDefault { get; set; }
}
