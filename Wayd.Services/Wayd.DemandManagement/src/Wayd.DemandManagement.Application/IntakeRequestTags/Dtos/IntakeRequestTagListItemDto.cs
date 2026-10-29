namespace Wayd.DemandManagement.Application.IntakeRequestTags.Dtos;

/// <summary>
/// One intake request tag, on an axis or free, with how many requests the reader can see carry it.
/// </summary>
public sealed record IntakeRequestTagListItemDto
{
    /// <summary>The tag's id, which a request refers to.</summary>
    public Guid Id { get; init; }

    /// <summary>The label.</summary>
    public required string Name { get; init; }

    /// <summary>What the label means, when described.</summary>
    public string? Description { get; init; }

    /// <summary>The tag's axis, or null for a free tag.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>What the company calls the tag's axis, or null for a free tag.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Whether this is a free tag: one on no axis, created by an agent typing its name while tagging.</summary>
    public bool IsFree { get; init; }

    /// <summary>Whether requests can still be given the tag. Always true for a free tag.</summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// How many requests the reader can see carry the tag. Counts only visible requests, so it never discloses a
    /// private desk's.
    /// </summary>
    public int RequestCount { get; init; }
}
