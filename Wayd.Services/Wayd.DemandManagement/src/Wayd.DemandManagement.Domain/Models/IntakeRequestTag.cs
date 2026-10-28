using Ardalis.GuardClauses;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// One label on one tag axis — "Payments" on Area, "Enterprise" on Customer segment.
/// </summary>
/// <remarks>
/// A row rather than text on the request, so a rename happens once, a picker lists the choices, and
/// "everything filed under Payments" is a join. Descriptive only: nothing in the domain branches on a tag.
/// Changed only through <see cref="IntakeRequestTagCategory"/>.
/// </remarks>
public sealed class IntakeRequestTag : BaseAuditableEntity
{
    private IntakeRequestTag() { }

    internal IntakeRequestTag(Guid categoryId, string name, string? description)
    {
        CategoryId = Guard.Against.Default(categoryId, nameof(categoryId));
        Name = name;
        Description = description;
    }

    /// <summary>The axis this tag belongs to.</summary>
    public Guid CategoryId { get; private init; }

    /// <summary>The label. Unique within its axis, ignoring case.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>What the label means, where it is not obvious.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Whether requests can still be given this tag. Requests already carrying it keep it.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    internal void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    internal void SetActive(bool isActive) => IsActive = isActive;
}
