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
public sealed class Product : BaseAuditableEntity, IHasIdAndKey, ISimpleProduct
{
    private Product() { }

    private Product(string name, string? description, Guid productTypeId, Guid? parentId, string? externalId, StatusRef status)
    {
        Name = name;
        Description = description;
        ProductTypeId = productTypeId;
        ParentId = parentId;
        ExternalId = externalId;
        StatusId = status.StatusId;
        StatusCategory = status.Category;
        StatusAlias = (ProductStatusAlias)status.Alias;
    }

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

    /// <summary>
    /// The parent node, or <c>null</c> for a root. Composition only — the provides/consumes graph is a
    /// separate relationship arriving in phase two, because an access surface spanning several products
    /// has no honest place in a single-parent tree.
    /// </summary>
    public Guid? ParentId { get; private set; }

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
    /// The lifecycle status this node currently holds, from its assigned workflow.
    /// </summary>
    public Guid StatusId { get; private set; }

    /// <summary>
    /// The status's category, denormalized so reads and invariants never need the workflow loaded.
    /// Kept in step with <see cref="StatusId"/> by every method that changes it.
    /// </summary>
    public StatusCategory StatusCategory { get; private set; }

    /// <summary>
    /// The well-known meaning of the current status, denormalized alongside it.
    /// </summary>
    /// <remarks>
    /// Stored so a lifecycle change can report the alias it moved <em>from</em>; see
    /// <see cref="ProductLifecycleChangedEvent.FromAlias"/>.
    /// </remarks>
    public ProductStatusAlias StatusAlias { get; private set; }

    /// <summary>
    /// Updates the node's name, description or external identifier.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches, so an unedited save records no change. Compares
    /// trimmed input because the setters trim.
    /// </remarks>
    public Result UpdateDetails(string name, string? description, string? externalId, EventActor actor, Instant timestamp)
    {
        var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
        var newDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var newExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

        if (string.Equals(Name, newName, StringComparison.Ordinal)
            && string.Equals(Description, newDescription, StringComparison.Ordinal)
            && string.Equals(ExternalId, newExternalId, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        Name = newName;
        Description = newDescription;
        ExternalId = newExternalId;

        AddDomainEvent(new ProductDetailsUpdatedEvent(Id, Key, Name, Description, ExternalId, actor, timestamp));

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
    /// <param name="hasReleases">
    /// Whether any release already exists for this node; supplied by the caller, which owns that query.
    /// </param>
    /// <param name="isTargetReleasable">Whether the target type permits releases.</param>
    public Result Retype(Guid productTypeId, bool isTargetReleasable, bool hasReleases, EventActor actor, Instant timestamp)
    {
        Guard.Against.Default(productTypeId, nameof(productTypeId));

        if (productTypeId == ProductTypeId)
        {
            return Result.Success();
        }

        if (hasReleases && !isTargetReleasable)
        {
            return Result.Failure("This product has releases and cannot be changed to a type that is not releasable.");
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

        StatusId = status.StatusId;
        StatusCategory = status.Category;
        StatusAlias = (ProductStatusAlias)status.Alias;

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
    /// <param name="hasReleases">Whether any release was ever cut against this node.</param>
    public Result Remove(bool hasChildren, bool hasReleases, EventActor actor, Instant timestamp)
    {
        if (hasChildren)
        {
            return Result.Failure("This product has child products and cannot be removed. Move or remove them first.");
        }

        if (hasReleases)
        {
            return Result.Failure("This product has releases and cannot be removed.");
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

        var product = new Product(name, description, productTypeId, parentId, externalId, initialStatus);

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
