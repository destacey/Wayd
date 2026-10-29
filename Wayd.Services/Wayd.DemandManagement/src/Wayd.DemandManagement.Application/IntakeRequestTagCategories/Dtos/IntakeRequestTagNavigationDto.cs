namespace Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;

/// <summary>
/// A tag an intake request carries, named with its axis, or a free tag on none.
/// </summary>
public sealed record IntakeRequestTagNavigationDto
{
    /// <summary>The tag's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The label.</summary>
    public required string Name { get; init; }

    /// <summary>The tag's axis, or null for a free tag.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>What the company calls the tag's axis, or null for a free tag.</summary>
    public string? CategoryName { get; init; }
}
