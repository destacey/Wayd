using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Planning.Domain.Models.Roadmaps;

/// <summary>
/// A named color configured on a Roadmap. Roadmap colors define the color vocabulary for a
/// Roadmap: each one pairs a color with a caption, and one can be marked as the default applied
/// to activities that have no color of their own. A Roadmap color has no identity of its own — it
/// is defined entirely by its values.
/// </summary>
public sealed class RoadmapColor : ValueObject
{
    private RoadmapColor() { }

    internal RoadmapColor(string color, string name, int order, bool isDefault)
    {
        Color = color;
        Name = name;
        Order = order;
        IsDefault = isDefault;
    }

    /// <summary>
    /// The color, stored as a hex code (e.g. "#4096FF"). The color is the natural key for a
    /// Roadmap color: a Roadmap cannot have two colors with the same hex.
    /// </summary>
    public string Color
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Color)).Trim();
    } = default!;

    /// <summary>
    /// The caption describing what the color represents on this Roadmap.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The order of the color within the Roadmap's configured colors.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this is the default color applied to activities that have no color of their own.
    /// At most one Roadmap color can be the default.
    /// </summary>
    public bool IsDefault { get; private set; }

    protected override IEnumerable<IComparable> GetEqualityComponents()
    {
        yield return Color;
        yield return Name;
        yield return Order;
        yield return IsDefault;
    }
}
