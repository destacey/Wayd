namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;

/// <summary>
/// A tag axis reviewers file intake requests along, with its tags.
/// </summary>
public sealed record IntakeRequestTagCategoryDto
{
    /// <summary>The axis's id.</summary>
    public Guid Id { get; init; }

    /// <summary>What the company calls the axis.</summary>
    public required string Name { get; init; }

    /// <summary>What the axis is for, when described.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether a request can carry several tags from the axis. When false, tagging a request replaces the tag
    /// it held on the axis. Fixed once the axis exists.
    /// </summary>
    public bool AllowsMany { get; init; }

    /// <summary>Position when presenting the axes, lowest first.</summary>
    public int Order { get; init; }

    /// <summary>Whether requests can still be tagged along the axis.</summary>
    public bool IsActive { get; init; }

    /// <summary>Whether the platform defines the axis, which then cannot be renamed, given tags or deleted.</summary>
    public bool IsSystem { get; init; }

    /// <summary>The axis's tags, alphabetically, inactive ones included.</summary>
    public required IReadOnlyList<IntakeRequestTagOptionDto> Tags { get; init; }
}

/// <summary>
/// One tag on an axis, as the settings page and the pickers list it.
/// </summary>
public sealed record IntakeRequestTagOptionDto
{
    /// <summary>The tag's id, which a request refers to.</summary>
    public Guid Id { get; init; }

    /// <summary>The label, unique within its axis.</summary>
    public required string Name { get; init; }

    /// <summary>What the label means, when described.</summary>
    public string? Description { get; init; }

    /// <summary>Whether requests can still be given the tag.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// How many requests the reader can see carry the tag, so an admin can see what deactivating or deleting it
    /// would touch. Counts only visible requests, so it never discloses a private desk's.
    /// </summary>
    public int RequestCount { get; init; }
}
