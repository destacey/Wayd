using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// An axis products are labelled along — Platform, Tech Stack, Compliance — holding the tags that
/// belong to it.
/// </summary>
/// <remarks>
/// Grouped because <c>ios</c>, <c>react-native</c> and <c>pci-scope</c> are different kinds of statement. The
/// type system deliberately carries only what changes a product's behaviour, and the axes carry everything else.
/// The rules are shared with every module's tag axes; see <see cref="TagCategoryBase{TTag}"/>.
/// </remarks>
public sealed class ProductTagCategory : TagCategoryBase<ProductTag>, IHasIdAndKey
{
    private readonly List<ProductTag> _tags = [];

    private ProductTagCategory() { }

    private ProductTagCategory(string name, string? description, bool allowsMany, int order, bool isSystem)
        : base(name, description, allowsMany, order, isSystem)
    {
    }

    /// <summary>
    /// The unique auto-generated key of the category. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>The failure for changing what a system axis means.</summary>
    public const string SystemAxisError = TagErrors.SystemAxis;

    /// <summary>The failure for a tag id that is not on this axis.</summary>
    public const string TagNotFoundError = TagErrors.TagNotFound;

    /// <summary>The failure for a tag with a blank name.</summary>
    public const string TagNameRequiredError = TagErrors.TagNameRequired;

    /// <summary>The failure for a tag name another tag on the axis already has.</summary>
    public const string DuplicateTagNameError = TagErrors.DuplicateTagName;

    /// <summary>The failure for deleting a tag a product carries.</summary>
    public const string TagInUseError = "Products carry this tag, so it cannot be deleted. Deactivate it instead.";

    /// <summary>The failure for deleting an axis products are tagged along.</summary>
    public const string AxisInUseError = "Products are tagged along this axis, so it cannot be deleted. Deactivate it instead.";

    /// <summary>
    /// Defines an axis the organization manages.
    /// </summary>
    public static ProductTagCategory Create(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp) =>
        Create(name, description, allowsMany, order, isSystem: false, actor, timestamp);

    /// <summary>
    /// Defines a platform-seeded axis. For the seeder.
    /// </summary>
    public static ProductTagCategory CreateSystem(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp) =>
        Create(name, description, allowsMany, order, isSystem: true, actor, timestamp);

    /// <summary>
    /// Adds a tag to a seeded axis, bypassing the read-only guard. For the seeder that builds it.
    /// </summary>
    public Result<ProductTag> AddSystemTag(string name, string? description, EventActor actor, Instant timestamp) =>
        AddTagUnguarded(name, description, actor, timestamp);

    private static ProductTagCategory Create(string name, string? description, bool allowsMany, int order,
        bool isSystem, EventActor actor, Instant timestamp)
    {
        var category = new ProductTagCategory(name, description, allowsMany, order, isSystem);
        category.RaiseCreated(actor, timestamp);

        return category;
    }

    protected override List<ProductTag> TagList => _tags;

    protected override string TagInUseMessage => TagInUseError;

    protected override string AxisInUseMessage => AxisInUseError;

    protected override ProductTag CreateTag(string name, string? description) => new(Id, name, description);

    protected override DomainEvent CreatedEvent(EventActor actor, Instant timestamp) =>
        new ProductTagCategoryCreatedEvent(Id, Name, Description, AllowsMany, Order, actor, timestamp);

    protected override DomainEvent DetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new ProductTagCategoryDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent ReorderedEvent(int fromOrder, EventActor actor, Instant timestamp) =>
        new ProductTagCategoryReorderedEvent(Id, fromOrder, Order, actor, timestamp);

    protected override DomainEvent ActivatedEvent(EventActor actor, Instant timestamp) =>
        new ProductTagCategoryActivatedEvent(Id, actor, timestamp);

    protected override DomainEvent DeactivatedEvent(EventActor actor, Instant timestamp) =>
        new ProductTagCategoryDeactivatedEvent(Id, actor, timestamp);

    protected override DomainEvent DeletedEvent(EventActor actor, Instant timestamp) =>
        new ProductTagCategoryDeletedEvent(Id, Name, actor, timestamp);

    protected override DomainEvent TagAddedEvent(ProductTag tag, EventActor actor, Instant timestamp) =>
        new ProductTagAddedEvent(Id, tag.Id, tag.Name, tag.Description, actor, timestamp);

    protected override DomainEvent TagDetailsUpdatedEvent(ProductTag tag, string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new ProductTagDetailsUpdatedEvent(Id, tag.Id, tag.Name, tag.Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent TagActivatedEvent(ProductTag tag, EventActor actor, Instant timestamp) =>
        new ProductTagActivatedEvent(Id, tag.Id, actor, timestamp);

    protected override DomainEvent TagDeactivatedEvent(ProductTag tag, EventActor actor, Instant timestamp) =>
        new ProductTagDeactivatedEvent(Id, tag.Id, actor, timestamp);

    protected override DomainEvent TagRemovedEvent(ProductTag tag, EventActor actor, Instant timestamp) =>
        new ProductTagRemovedEvent(Id, tag.Id, tag.Name, actor, timestamp);
}
