using Ardalis.GuardClauses;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// One label a module's records can carry — <c>ios</c> on a product's Platform axis, "Payments" on an intake
/// request's Area axis.
/// </summary>
/// <remarks>
/// A row rather than text on the record, so a rename happens once, a picker lists the choices, and "everything
/// tagged ios" is a join. Descriptive only: nothing in the domain branches on a tag. Each module derives its own
/// tag, with its own table; modules never share tags. Changed only through its <see cref="TagCategoryBase{TTag}"/>.
/// </remarks>
public abstract class TagBase : BaseAuditableEntity
{
    protected TagBase() { }

    protected TagBase(Guid categoryId, string name, string? description)
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
    /// Whether records can still be given this tag. Records already carrying it keep it.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    internal void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    internal void SetActive(bool isActive) => IsActive = isActive;
}
