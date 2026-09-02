using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Interfaces.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A node in the product taxonomy — a product line, a product, a service, an application, a tool, or a
/// module inside one. Self-referencing, so one tree serves both the commercial and the technical
/// audience, with the node's <see cref="ProductTypeId"/> deciding what it is allowed to do.
/// </summary>
public sealed class Product : StatusTrackedEntity, IHasIdAndKey, ISimpleProduct
{
    private readonly List<ProductTagAssignment> _tags = [];

    private Product() { }

    private Product(string name, string? description, Guid productTypeId, Guid? parentId, string? externalId)
    {
        Name = name;
        Description = description;
        ProductTypeId = productTypeId;
        ParentId = parentId;
        ExternalId = externalId;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.Product.Key;

    /// <summary>
    /// The unique auto-generated key of the product. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the product node.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// What the node is and why it exists.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The node's type, which decides whether it can carry releases.
    /// </summary>
    public Guid ProductTypeId { get; private set; }

    /// <summary>The node's type, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public ProductType? ProductType { get; private init; }

    /// <summary>
    /// The parent node, or <c>null</c> for a root. Composition only — the provides/consumes graph is a
    /// separate relationship arriving in phase two, because an access surface spanning several products
    /// has no honest place in a single-parent tree.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>The node this one sits under, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public Product? Parent { get; private init; }

    /// <summary>
    /// The node's identifier in whatever system owns it — a repository, a pipeline, a registry package.
    /// </summary>
    /// <remarks>
    /// Captured from the start though nothing consumes it yet: reconciling hand-curated nodes against a
    /// later automated feed is a matching problem with these and a re-authoring problem without.
    /// </remarks>
    public string? ExternalId
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The well-known meaning of the current status.
    /// </summary>
    public ProductStatusAlias StatusAlias => (ProductStatusAlias)StatusAliasValue;

    /// <summary>
    /// The tags this node carries, across every axis.
    /// </summary>
    /// <remarks>
    /// Where the type system stops. A type decides what a node may <em>do</em> — whether releases can be
    /// cut against it — and tags describe everything else, so the two never compete: web and mobile
    /// applications behave identically and differ only by label.
    /// </remarks>
    public IReadOnlyCollection<ProductTagAssignment> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Applies a tag.
    /// </summary>
    /// <param name="category">
    /// The tag's axis, supplied by the caller because the aggregate cannot load it. Its
    /// <see cref="ProductTagCategory.AllowsMany"/> decides whether this replaces an existing tag on the
    /// same axis or joins it.
    /// </param>
    public Result Tag(ProductTag tag, ProductTagCategory category, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(tag, nameof(tag));
        Guard.Against.Null(category, nameof(category));

        if (tag.CategoryId != category.Id)
        {
            return Result.Failure("That tag does not belong to the supplied axis.");
        }

        if (!tag.IsActive || !category.IsActive)
        {
            return Result.Failure("An inactive tag cannot be applied.");
        }

        if (_tags.Any(t => t.TagId == tag.Id))
        {
            return Result.Success();
        }

        // A single-value axis holds one tag: applying another replaces it rather than failing, since
        // "this is a mobile app, not a web app" is a correction, not an error.
        if (!category.AllowsMany)
        {
            _tags.RemoveAll(t => t.CategoryId == category.Id);
        }

        _tags.Add(new ProductTagAssignment(Id, tag.Id, category.Id));

        AddDomainEvent(new ProductTagsChangedEvent(Id, Key, Name, [.. _tags.Select(t => t.TagId)], actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes a tag. Succeeds whether or not the node carried it.
    /// </summary>
    public Result Untag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (_tags.RemoveAll(t => t.TagId == tagId) == 0)
        {
            return Result.Success();
        }

        AddDomainEvent(new ProductTagsChangedEvent(Id, Key, Name, [.. _tags.Select(t => t.TagId)], actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Updates the node's name or description.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches, so an unedited save records no change. Compares
    /// trimmed input because the setters trim.
    /// </remarks>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
        var newDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Description, newDescription, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        Name = newName;
        Description = newDescription;

        AddDomainEvent(new ProductDetailsUpdatedEvent(Id, Key, Name, Description, ExternalId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Points the node at the record that owns it in another system, or clears the link.
    /// </summary>
    /// <remarks>
    /// Separate from a details edit because it answers a different question — not what this product is
    /// called, but which repository, pipeline or registry package it corresponds to. Keeping it here also
    /// keeps a rename from having to restate the link, which a caller that forgot would silently clear.
    /// </remarks>
    /// <param name="externalId">The identifier in the owning system, or <c>null</c> to unlink.</param>
    public Result LinkExternally(string? externalId, EventActor actor, Instant timestamp)
    {
        var newExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

        if (string.Equals(ExternalId, newExternalId, StringComparison.Ordinal))
            return Result.Success();

        ExternalId = newExternalId;

        AddDomainEvent(new ProductLinkedExternallyEvent(Id, Key, Name, Description, ExternalId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the node under a different parent, or to the root.
    /// </summary>
    /// <param name="parentId">The new parent, or <c>null</c> to make this a root node.</param>
    /// <param name="ancestorIds">
    /// The new parent's ancestors, nearest first. Empty when <paramref name="parentId"/> is
    /// <c>null</c>, and only then.
    /// </param>
    /// <remarks>
    /// <strong>The cycle check is only as good as <paramref name="ancestorIds"/>.</strong> Passing an
    /// empty collection for a non-null parent silently disables it; the domain cannot query the tree to
    /// notice. Only the self-parent case is caught unconditionally.
    /// </remarks>
    public Result Reparent(Guid? parentId, IReadOnlyCollection<Guid> ancestorIds, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(ancestorIds, nameof(ancestorIds));

        if (parentId == Id)
        {
            return Result.Failure("A product cannot be its own parent.");
        }

        if (parentId is not null && ancestorIds.Contains(Id))
        {
            return Result.Failure("A product cannot be moved beneath one of its own descendants.");
        }

        if (parentId == ParentId)
        {
            return Result.Success();
        }

        var fromParentId = ParentId;
        ParentId = parentId;

        AddDomainEvent(new ProductReparentedEvent(Id, Key, Name, fromParentId, parentId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes the node's type.
    /// </summary>
    /// <param name="hasVersions">
    /// Whether any version already exists for this node; supplied by the caller, which owns that query.
    /// </param>
    /// <param name="isTargetReleasable">Whether the target type permits versions.</param>
    public Result Retype(Guid productTypeId, bool isTargetReleasable, bool hasVersions, EventActor actor, Instant timestamp)
    {
        Guard.Against.Default(productTypeId, nameof(productTypeId));

        if (productTypeId == ProductTypeId)
        {
            return Result.Success();
        }

        if (hasVersions && !isTargetReleasable)
        {
            return Result.Failure("This product has versions and cannot be changed to a type that is not releasable.");
        }

        var fromProductTypeId = ProductTypeId;
        ProductTypeId = productTypeId;

        AddDomainEvent(new ProductRetypedEvent(Id, Key, Name, fromProductTypeId, productTypeId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the node to a different lifecycle status.
    /// </summary>
    /// <remarks>
    /// One method rather than named <c>Sunset</c>/<c>Retire</c> methods: statuses are configurable, so a
    /// fixed set could not reach one an organization invented. The event carries the target's alias.
    /// </remarks>
    public Result ChangeStatus(StatusRef status, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(status, nameof(status));

        if (status.StatusId == StatusId)
        {
            return Result.Success();
        }

        var fromStatusId = StatusId;
        var fromCategory = StatusCategory;
        var fromAlias = StatusAlias;

        ApplyStatus(status, actor, timestamp);

        AddDomainEvent(new ProductLifecycleChangedEvent(
            Id, Key, Name,
            fromStatusId, fromCategory, fromAlias,
            status.StatusId, status.Category, (ProductStatusAlias)status.Alias,
            actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Raises the removal event. The caller performs the delete; this records why and for whom.
    /// </summary>
    /// <param name="hasChildren">Whether any node still hangs from this one.</param>
    /// <param name="hasVersions">Whether any version was ever cut against this node.</param>
    public Result Remove(bool hasChildren, bool hasVersions, bool isInAManifest, EventActor actor, Instant timestamp)
    {
        if (hasChildren)
        {
            return Result.Failure("This product has child products and cannot be removed. Move or remove them first.");
        }

        if (hasVersions)
        {
            return Result.Failure("This product has versions and cannot be removed.");
        }

        // Separate from hasVersions: a carried-forward component often has no version row at all, so a
        // product named only in a manifest passes that check and then hits the restricting foreign key,
        // where the failure surfaces as an unreadable generic error.
        if (isInAManifest)
        {
            return Result.Failure("This product appears in a release package manifest and cannot be removed.");
        }

        AddDomainEvent(new ProductRemovedEvent(Id, Key, Name, ParentId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Creates a product node.
    /// </summary>
    /// <param name="initialStatus">
    /// The starting status, resolved from the assigned workflow by the caller — the domain requires one
    /// and never resolves it, as with the actor.
    /// </param>
    public static Product Create(
        string name,
        string? description,
        Guid productTypeId,
        Guid? parentId,
        string? externalId,
        StatusRef initialStatus,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Default(productTypeId, nameof(productTypeId));
        Guard.Against.Null(initialStatus, nameof(initialStatus));

        var product = new Product(name, description, productTypeId, parentId, externalId);
        product.ApplyStatus(initialStatus, actor, timestamp);

        // Deferred because Key is database-generated: an event raised here would carry Key 0.
        product.AddPostPersistenceAction(() => product.AddDomainEvent(new ProductAddedEvent(
            product,
            product.ProductTypeId,
            product.ParentId,
            product.StatusId,
            product.StatusCategory,
            actor,
            timestamp)));

        return product;
    }
}
