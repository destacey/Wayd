using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// An axis products are labelled along — Platform, Tech Stack, Compliance — holding the tags that
/// belong to it.
/// </summary>
/// <remarks>
/// Tags are grouped rather than kept in one flat list because <c>ios</c>, <c>react-native</c> and
/// <c>pci-scope</c> are different kinds of statement. Flattened, "what platform is this?" needs the
/// caller to already know which values are platforms; grouped, it is a query. It also lets a picker
/// present one axis at a time rather than everything at once.
/// <para>
/// The axes an organization needs are not predictable — the type system deliberately carries only what
/// changes behaviour, and this carries everything else — so they are data rather than an enum.
/// </para>
/// </remarks>
public sealed class ProductTagCategory : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<ProductTag> _tags = [];

    private ProductTagCategory() { }

    private ProductTagCategory(string name, string? description, bool allowsMany, int order, bool isSystem)
    {
        Name = name;
        Description = description;
        AllowsMany = allowsMany;
        Order = order;
        IsSystem = isSystem;
    }

    /// <summary>
    /// The unique auto-generated key of the category. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>What the organization calls this axis.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>What the axis is for, shown when choosing tags.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Whether a product can carry several tags from this axis.
    /// </summary>
    /// <remarks>
    /// True for Platform, because a cross-platform app genuinely targets iOS and Android and forcing a
    /// choice would record something false. An axis where a node can only be one thing sets this false
    /// and the domain refuses a second.
    /// </remarks>
    public bool AllowsMany { get; private set; }

    /// <summary>Display position when presenting the axes. Presentation only.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this is a platform-seeded axis. System categories cannot be edited or deleted; an
    /// organization wanting different axes adds its own.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether products can still be tagged along this axis.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The tags on this axis, in no particular order.
    /// </summary>
    /// <remarks>
    /// A set, not a sequence: a tag's position carries no meaning, so presenting them is the caller's
    /// business — and every caller so far wants them alphabetically, which the order they were added
    /// in would not give.
    /// </remarks>
    public IReadOnlyCollection<ProductTag> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Adds a tag to this axis.
    /// </summary>
    public Result<ProductTag> AddTag(string name, string? description = null)
    {
        if (IsSystem)
        {
            return Result.Failure<ProductTag>("System tag categories cannot be modified.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ProductTag>("A tag must have a name.");
        }

        var trimmed = name.Trim();

        if (_tags.Any(t => string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<ProductTag>($"A tag named '{trimmed}' already exists on this axis.");
        }

        var tag = new ProductTag(Id, trimmed, description);
        _tags.Add(tag);

        return Result.Success(tag);
    }

    /// <summary>
    /// Renames a tag on this axis.
    /// </summary>
    /// <remarks>
    /// On the category rather than on <see cref="ProductTag"/> because the rule a rename can break —
    /// no two tags on one axis sharing a name — is the category's to enforce; the tag cannot see its
    /// siblings.
    /// </remarks>
    public Result RenameTag(Guid tagId, string name, string? description = null)
    {
        if (IsSystem)
        {
            return Result.Failure("System tag categories cannot be modified.");
        }

        var tag = _tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null)
        {
            return Result.Failure("That tag does not belong to this axis.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("A tag must have a name.");
        }

        var trimmed = name.Trim();

        if (_tags.Any(t => t.Id != tagId && string.Equals(t.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure($"A tag named '{trimmed}' already exists on this axis.");
        }

        tag.Rename(trimmed, description);

        return Result.Success();
    }

    /// <summary>
    /// Takes one of the axis's tags out of use, or puts it back.
    /// </summary>
    /// <remarks>
    /// On the category for the same reason as <see cref="RenameTag"/>, though for a different rule:
    /// the system flag lives here and a tag cannot see it. Reaching the tag directly would let a
    /// seeded tag be deactivated, leaving products untaggable along that axis with no way to restore
    /// it — the seeder does not re-add it.
    /// </remarks>
    public Result SetTagActive(Guid tagId, bool isActive)
    {
        if (IsSystem)
        {
            return Result.Failure("System tag categories cannot be modified.");
        }

        var tag = _tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null)
        {
            return Result.Failure("That tag does not belong to this axis.");
        }

        return isActive ? tag.Activate() : tag.Deactivate();
    }

    /// <summary>Renames the axis.</summary>
    /// <remarks>
    /// Position is not editable here — see <see cref="SetOrder"/>. Ordering an axis is a statement about
    /// the whole list, so it arrives as one, rather than as a number each edit has to guess right.
    /// </remarks>
    public Result Update(string name, string? description)
    {
        if (IsSystem)
        {
            return Result.Failure("System tag categories cannot be modified.");
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    /// <summary>Moves the axis to a position in the list.</summary>
    /// <remarks>
    /// Deliberately not guarded by <see cref="IsSystem"/>, unlike everything else that writes to a
    /// category. The guard protects what a seeded axis <em>means</em> — its name, its tags, whether it
    /// takes many — none of which this touches. Where it sits among the others is the organization's
    /// call, and refusing it would pin every seeded axis above the organization's own for good.
    /// </remarks>
    public void SetOrder(int order) => Order = order;

    /// <summary>
    /// Takes the axis out of use, so nothing new can be tagged along it.
    /// </summary>
    /// <remarks>
    /// Products already tagged keep their tags — this is deactivation, not deletion, for the same reason
    /// a product type is: the labels stay meaningful on what already carries them.
    /// </remarks>
    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure("This tag category is already inactive.");
        }

        IsActive = false;

        return Result.Success();
    }

    /// <summary>Puts the axis back into use.</summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Failure("This tag category is already active.");
        }

        IsActive = true;

        return Result.Success();
    }

    /// <summary>Creates an organization-defined axis.</summary>
    public static ProductTagCategory Create(string name, string? description, bool allowsMany, int order) =>
        new(name, description, allowsMany, order, isSystem: false);

    /// <summary>Creates a platform-seeded axis.</summary>
    public static ProductTagCategory CreateSystem(string name, string? description, bool allowsMany, int order) =>
        new(name, description, allowsMany, order, isSystem: true);

    /// <summary>
    /// Adds a tag to a seeded axis, bypassing the read-only guard. For the seeder that builds it.
    /// </summary>
    public ProductTag AddSystemTag(string name, string? description = null)
    {
        var tag = new ProductTag(Id, name, description);
        _tags.Add(tag);

        return tag;
    }
}
