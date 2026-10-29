using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;

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
/// <para>
/// Tags are reached only through their axis, because the rules a tag change can break — no two tags on one
/// axis sharing a name, a system axis staying as seeded — are the axis's to enforce.
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
    /// Whether a product can carry several tags from this axis. Fixed once the axis exists.
    /// </summary>
    /// <remarks>
    /// True for Platform, because a cross-platform app genuinely targets iOS and Android and forcing a
    /// choice would record something false. On an axis that takes one tag, tagging a product replaces the
    /// tag it held rather than refusing, because choosing another value is a correction, not an error.
    /// </remarks>
    public bool AllowsMany { get; private init; }

    /// <summary>Display position when presenting the axes, lowest first. Presentation only.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this is a platform-seeded axis. A system axis cannot be renamed, given tags or deleted, and
    /// its tags cannot be changed; it can still be reordered and deactivated, which are the organization's
    /// calls.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether products can still be tagged along this axis. Products already tagged keep their tags.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The tags on this axis, in no particular order: a tag holds no position, so whoever presents them sorts
    /// them.
    /// </summary>
    public IReadOnlyCollection<ProductTag> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Renames the axis or changes its description.
    /// </summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        AddDomainEvent(new ProductTagCategoryDetailsUpdatedEvent(Id, Name, Description, previousName,
            previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the axis to another position in the list.
    /// </summary>
    /// <remarks>
    /// Allowed on a system axis: where it sits among the others is the organization's call, and refusing
    /// it would pin every seeded axis above the organization's own for good.
    /// </remarks>
    public Result Reorder(int order, EventActor actor, Instant timestamp)
    {
        if (order == Order)
            return Result.Success();

        var fromOrder = Order;
        Order = order;

        AddDomainEvent(new ProductTagCategoryReorderedEvent(Id, fromOrder, Order, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes the axis out of use: nothing new is tagged along it, and products already tagged keep their tags.
    /// </summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new ProductTagCategoryDeactivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Puts the axis back into use.
    /// </summary>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (IsActive)
            return Result.Success();

        IsActive = true;
        AddDomainEvent(new ProductTagCategoryActivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the axis is being deleted, or refuses. Removing it, and its tags with it, is the caller's.
    /// </summary>
    /// <param name="isInUse">Whether any product carries a tag from this axis, which the axis cannot see.</param>
    public Result Delete(bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (isInUse)
            return Result.Failure(AxisInUseError);

        AddDomainEvent(new ProductTagCategoryDeletedEvent(Id, Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Adds a tag to the axis. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result<ProductTag> AddTag(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure<ProductTag>(SystemAxisError);

        return AddTagCore(name, description, actor, timestamp);
    }

    /// <summary>
    /// Renames one of the axis's tags or changes its description. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// Products refer to the tag by id, so every product carrying it reads the new name at once.
    /// </remarks>
    public Result UpdateTagDetails(Guid tagId, string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(TagNameRequiredError);

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: tagId))
            return Result.Failure(DuplicateTagNameError);

        var previousName = tag.Name;
        var previousDescription = tag.Description;

        tag.UpdateDetails(trimmed, description);

        if (tag.Name == previousName && tag.Description == previousDescription)
            return Result.Success();

        AddDomainEvent(new ProductTagDetailsUpdatedEvent(Id, tag.Id, tag.Name, tag.Description, previousName,
            previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes one of the axis's tags out of use. Products already carrying it keep it and can still have it
    /// removed. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// Refused on a system axis: a seeded tag deactivated would leave products untaggable with it and no
    /// way to restore it, because the seeder does not re-add it.
    /// </remarks>
    public Result DeactivateTag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (!tag.IsActive)
            return Result.Success();

        tag.SetActive(false);
        AddDomainEvent(new ProductTagDeactivatedEvent(Id, tag.Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Puts one of the axis's tags back into use. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result ActivateTag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (tag.IsActive)
            return Result.Success();

        tag.SetActive(true);
        AddDomainEvent(new ProductTagActivatedEvent(Id, tag.Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes one of the axis's tags outright. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// A tag a product carries is deactivated instead, which stops new use without stripping it from the
    /// products labelled with it.
    /// </remarks>
    /// <param name="isInUse">Whether any product carries the tag, which the axis cannot see.</param>
    public Result RemoveTag(Guid tagId, bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (isInUse)
            return Result.Failure(TagInUseError);

        _tags.Remove(tag);
        AddDomainEvent(new ProductTagRemovedEvent(Id, tag.Id, tag.Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>The failure for changing what a system axis means.</summary>
    public const string SystemAxisError = "System tag axes cannot be changed.";

    /// <summary>The failure for a tag id that is not on this axis.</summary>
    public const string TagNotFoundError = "That tag does not belong to this axis.";

    /// <summary>The failure for a tag with a blank name.</summary>
    public const string TagNameRequiredError = "A tag must have a name.";

    /// <summary>The failure for a tag name another tag on the axis already has.</summary>
    public const string DuplicateTagNameError = "A tag with this name already exists on this axis.";

    /// <summary>The failure for deleting a tag a product carries.</summary>
    public const string TagInUseError = "Products carry this tag, so it cannot be deleted. Deactivate it instead.";

    /// <summary>The failure for deleting an axis products are tagged along.</summary>
    public const string AxisInUseError = "Products are tagged along this axis, so it cannot be deleted. Deactivate it instead.";

    private Result<ProductTag> AddTagCore(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ProductTag>(TagNameRequiredError);

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: null))
            return Result.Failure<ProductTag>(DuplicateTagNameError);

        var tag = new ProductTag(Id, trimmed, description);
        _tags.Add(tag);

        AddDomainEvent(new ProductTagAddedEvent(Id, tag.Id, tag.Name, tag.Description, actor, timestamp));

        return tag;
    }

    private ProductTag? FindTag(Guid tagId) => _tags.FirstOrDefault(t => t.Id == tagId);

    private bool HasTagNamed(string name, Guid? exceptTagId) =>
        _tags.Any(t => t.Id != exceptTagId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Defines an axis the organization manages.
    /// </summary>
    public static ProductTagCategory Create(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp) =>
        CreateCore(name, description, allowsMany, order, isSystem: false, actor, timestamp);

    /// <summary>
    /// Defines a platform-seeded axis. For the seeder.
    /// </summary>
    public static ProductTagCategory CreateSystem(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp) =>
        CreateCore(name, description, allowsMany, order, isSystem: true, actor, timestamp);

    /// <summary>
    /// Adds a tag to a seeded axis, bypassing the read-only guard. For the seeder that builds it.
    /// </summary>
    public Result<ProductTag> AddSystemTag(string name, string? description, EventActor actor, Instant timestamp) =>
        AddTagCore(name, description, actor, timestamp);

    private static ProductTagCategory CreateCore(string name, string? description, bool allowsMany, int order,
        bool isSystem, EventActor actor, Instant timestamp)
    {
        var category = new ProductTagCategory(name, description, allowsMany, order, isSystem);

        category.AddDomainEvent(new ProductTagCategoryCreatedEvent(category.Id, category.Name,
            category.Description, category.AllowsMany, category.Order, actor, timestamp));

        return category;
    }
}
