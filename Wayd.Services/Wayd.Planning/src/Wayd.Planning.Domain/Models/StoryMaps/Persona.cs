using Ardalis.GuardClauses;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A persona defined on a Story Map. Personas are defined per map — a persona created on one map
/// does not appear on another. Goals, steps, and tasks can be tagged with a persona, and the map
/// can be filtered to a single persona to focus the view.
/// </summary>
public sealed class Persona : BaseAuditableEntity
{
    private Persona() { }

    internal Persona(Guid storyMapId, string name, string? description, string color, int order)
    {
        StoryMapId = storyMapId;
        Name = name;
        Description = description;
        Color = color;
        Order = order;
    }

    /// <summary>
    /// The Story Map this persona belongs to.
    /// </summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>
    /// The name of the persona.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// An optional one-line description of the persona.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The color assigned to the persona, stored as a hex code (e.g. "#4096FF"). Colors are chosen
    /// from a fixed palette; inline creation assigns one automatically.
    /// </summary>
    public string Color
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Color)).Trim();
    } = default!;

    /// <summary>
    /// The order of the persona within the map's persona list, controlling the filter-bar sequence.
    /// </summary>
    public int Order { get; private set; }

    internal void Update(string name, string? description, string color)
    {
        Name = name;
        Description = description;
        Color = color;
    }

    internal void SetOrder(int order) => Order = order;
}
