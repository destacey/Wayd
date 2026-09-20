using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Interfaces.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A node in the product taxonomy — a product line, a product, a service, an application, a tool, or a
/// module inside one. Self-referencing, so one tree serves both the commercial and the technical
/// audience, with the node's <see cref="ProductTypeId"/> deciding what it is allowed to do.
/// </summary>
public sealed class Product : StatusTrackedEntity, IHasIdAndKey, ISimpleProduct
{
    private readonly List<ProductTagAssignment> _tags = [];
    private readonly List<ProductDependency> _dependencies = [];

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
        Guid[] removed = [];
        if (!category.AllowsMany)
        {
            removed = [.. _tags.Where(t => t.CategoryId == category.Id).Select(t => t.TagId)];
            _tags.RemoveAll(t => t.CategoryId == category.Id);
        }

        _tags.Add(new ProductTagAssignment(Id, tag.Id, category.Id));

        AddDomainEvent(new ProductTagsChangedEventV2(Id, Key, [tag.Id], removed, [.. _tags.Select(t => t.TagId)], actor, timestamp));

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

        AddDomainEvent(new ProductTagsChangedEventV2(Id, Key, [], [tagId], [.. _tags.Select(t => t.TagId)], actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The products this one depends on, ended links included.
    /// </summary>
    /// <remarks>
    /// Only the dependent side. What depends on this product is a query over other products' links, and its
    /// Activity learns of them through the events naming it as a related aggregate.
    /// </remarks>
    public IReadOnlyCollection<ProductDependency> Dependencies => _dependencies.AsReadOnly();

    /// <summary>
    /// Records that this product depends on another from <paramref name="startsOn"/>.
    /// </summary>
    /// <param name="ancestorIds">This product's ancestors. Including the product itself is harmless.</param>
    /// <param name="dependsOnAncestorIds">
    /// The other product's ancestors. Including that product itself is harmless.
    /// </param>
    /// <param name="today">The current date, which no dependency may start after.</param>
    /// <remarks>
    /// <strong>The composition check is only as good as the two ancestries.</strong> Passing empty collections
    /// silently lets a product depend on its own parent or child; the domain cannot query the tree to notice.
    /// <para>
    /// A product and anything above or below it in the tree is composition, which the tree already records.
    /// A dependency on the same product may be recorded again once the earlier one ended, but never over any
    /// day one already covers.
    /// </para>
    /// </remarks>
    public Result<ProductDependency> AddDependency(
        Guid dependsOnProductId,
        DependencyStrength strength,
        InteractionStyle? interactionStyle,
        string? description,
        LocalDate startsOn,
        IReadOnlyCollection<Guid> ancestorIds,
        IReadOnlyCollection<Guid> dependsOnAncestorIds,
        LocalDate today,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Default(dependsOnProductId, nameof(dependsOnProductId));
        Guard.Against.EnumOutOfRange(strength, nameof(strength));
        Guard.Against.Null(ancestorIds, nameof(ancestorIds));
        Guard.Against.Null(dependsOnAncestorIds, nameof(dependsOnAncestorIds));

        if (dependsOnProductId == Id)
        {
            return Result.Failure<ProductDependency>("A product cannot depend on itself.");
        }

        if (ancestorIds.Contains(dependsOnProductId) || dependsOnAncestorIds.Contains(Id))
        {
            return Result.Failure<ProductDependency>(
                "A product cannot depend on a product above or below it in the product tree. That relationship is composition, which the tree already records.");
        }

        return OpenDependency(dependsOnProductId, strength, interactionStyle, description, startsOn, today, actor, timestamp);
    }

    /// <summary>
    /// Records that a dependency stopped, <paramref name="endsOn"/> being the last day it held. The link is kept.
    /// </summary>
    /// <param name="today">The current date, which no dependency may end after.</param>
    /// <remarks>
    /// May end on the day it started: a dependency that held for a single day still held.
    /// </remarks>
    public Result EndDependency(Guid dependencyId, LocalDate endsOn, LocalDate today, EventActor actor, Instant timestamp)
    {
        var dependency = _dependencies.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return Result.Failure("Dependency not found.");
        }

        if (!dependency.IsOpen)
        {
            return Result.Failure("This dependency has already ended.");
        }

        if (endsOn < dependency.Period.Start)
        {
            return Result.Failure("A dependency cannot end before it started.");
        }

        if (endsOn > today)
        {
            return Result.Failure("A dependency cannot end in the future.");
        }

        Close(dependency, endsOn, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Changes the terms a dependency holds on — whether this product stops working without the one it
    /// depends on, how it reaches it, or both — from <paramref name="changedOn"/>.
    /// </summary>
    /// <param name="today">The current date, which no change may come after.</param>
    /// <remarks>
    /// Ends the current link the day before and opens another carrying the new terms from
    /// <paramref name="changedOn"/>, rather than editing it: editing would re-judge downtime before the change by
    /// terms that did not hold then. Periods include their end day, so the two links meet without sharing a
    /// day. That leaves no day to end on when the change falls on the day the link started, which is refused: a
    /// link recorded on the wrong terms is removed and added again. The new link keeps the description, and
    /// is not re-checked against the tree — it continues a dependency already recorded.
    /// <para>
    /// Recording styles on a link that had none is the exception, and fills them in place: nothing about the
    /// dependency changed, somebody finally wrote down how it had always worked, so dating it would split the
    /// period on a day nothing happened. A null <paramref name="interactionStyle"/> leaves recorded styles
    /// alone rather than clearing them — unrecording a fact is not a change of terms.
    /// </para>
    /// </remarks>
    public Result<ProductDependency> ChangeDependencyTerms(
        Guid dependencyId, DependencyStrength strength, InteractionStyle? interactionStyle, LocalDate changedOn, LocalDate today, EventActor actor, Instant timestamp)
    {
        Guard.Against.EnumOutOfRange(strength, nameof(strength));

        var dependency = _dependencies.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return Result.Failure<ProductDependency>("Dependency not found.");
        }

        if (!dependency.IsOpen)
        {
            return Result.Failure<ProductDependency>("An ended dependency cannot change terms.");
        }

        var strengthChanged = dependency.Strength != strength;
        var stylesChanged = interactionStyle is not null
            && dependency.InteractionStyle is not null
            && dependency.InteractionStyle != interactionStyle;

        if (!strengthChanged && !stylesChanged)
        {
            // Whatever is left is either nothing at all or styles being written down for the first time.
            return RecordDependencyDetails(dependency, dependency.Description, interactionStyle, actor, timestamp)
                .Map(() => dependency);
        }

        if (changedOn <= dependency.Period.Start)
        {
            return Result.Failure<ProductDependency>(
                "A dependency's terms can change from the day after it started. If it was recorded on the wrong terms, remove it and add it again.");
        }

        if (changedOn > today)
        {
            return Result.Failure<ProductDependency>("A dependency's terms cannot change in the future.");
        }

        Close(dependency, changedOn.PlusDays(-1), actor, timestamp);

        return OpenDependency(
            dependency.DependsOnProductId, strength, interactionStyle ?? dependency.InteractionStyle,
            dependency.Description, changedOn, today, actor, timestamp);
    }

    /// <summary>
    /// Rewords what a dependency is for, and records the styles it uses where none were recorded. Allowed on
    /// an ended link, since both describe the link rather than asserting anything about when it held.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches. Compares after assignment because the setter trims.
    /// <para>
    /// A null <paramref name="interactionStyle"/> leaves recorded styles alone, where a null description
    /// clears one. The asymmetry is deliberate: a description is prose a caller may genuinely want to empty,
    /// whereas styles are read when downtime is attributed, and a caller that simply omitted the field would
    /// otherwise erase them. Changing styles already recorded is a change of terms, not a correction, so it
    /// is refused here — see <see cref="ChangeDependencyTerms"/>.
    /// </para>
    /// </remarks>
    public Result UpdateDependencyDetails(Guid dependencyId, string? description, InteractionStyle? interactionStyle, EventActor actor, Instant timestamp)
    {
        var dependency = _dependencies.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return Result.Failure("Dependency not found.");
        }

        return RecordDependencyDetails(dependency, description, interactionStyle, actor, timestamp);
    }

    private Result RecordDependencyDetails(
        ProductDependency dependency, string? description, InteractionStyle? interactionStyle, EventActor actor, Instant timestamp)
    {
        if (interactionStyle is not null
            && dependency.InteractionStyle is not null
            && dependency.InteractionStyle != interactionStyle)
        {
            return Result.Failure(
                "This dependency's interaction styles have already been recorded. Changing them is a change of terms, which ends this dependency and starts another.");
        }

        var previousDescription = dependency.Description;
        var previousStyles = dependency.InteractionStyle;

        dependency.Description = description;

        if (interactionStyle is not null)
        {
            dependency.InteractionStyle = interactionStyle;
        }

        if (dependency.Description == previousDescription && dependency.InteractionStyle == previousStyles)
        {
            return Result.Success();
        }

        AddDomainEvent(new ProductDependencyDetailsUpdatedEvent(
            Id, Key, dependency.Id, dependency.DependsOnProductId, dependency.Description, previousDescription,
            dependency.InteractionStyle.ToFlags(), previousStyles.ToFlags(), actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Deletes a dependency that was recorded by mistake.
    /// </summary>
    /// <remarks>
    /// Not how a dependency that stopped is recorded — that is <see cref="EndDependency"/>. Ending a link that
    /// was never true would leave a history asserting a dependency existed, which is exactly what attributing
    /// downtime later relies on. A reason is required because this contradicts that history.
    /// </remarks>
    public Result RemoveDependency(Guid dependencyId, string reason, EventActor actor, Instant timestamp)
    {
        var dependency = _dependencies.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return Result.Failure("Dependency not found.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure("A reason is required to remove a dependency.");
        }

        _dependencies.Remove(dependency);

        AddDomainEvent(new ProductDependencyRemovedEvent(
            Id, Key, dependency.Id, dependency.DependsOnProductId, dependency.Strength,
            dependency.InteractionStyle.ToFlags(), dependency.Period, reason.Trim(), actor, timestamp));

        return Result.Success();
    }

    private Result<ProductDependency> OpenDependency(
        Guid dependsOnProductId, DependencyStrength strength, InteractionStyle? interactionStyle, string? description, LocalDate startsOn, LocalDate today, EventActor actor, Instant timestamp)
    {
        if (startsOn > today)
        {
            return Result.Failure<ProductDependency>("A dependency cannot start in the future.");
        }

        var onSameProduct = _dependencies.Where(d => d.DependsOnProductId == dependsOnProductId).ToList();

        if (onSameProduct.Any(d => d.IsOpen))
        {
            return Result.Failure<ProductDependency>(
                "This product already depends on that product. End the current dependency before recording another.");
        }

        // Every other link on the product has ended, and an open link from startsOn overlaps any of them that
        // held on or after that day.
        if (onSameProduct.Any(d => d.Period.End >= startsOn))
        {
            return Result.Failure<ProductDependency>(
                "An earlier dependency on that product already covers part of this period. Start this one after the day it ended.");
        }

        var dependency = new ProductDependency(Id, dependsOnProductId, strength, interactionStyle, description, startsOn);
        _dependencies.Add(dependency);

        AddDomainEvent(new ProductDependencyAddedEvent(
            Id, Key, dependency.Id, dependency.DependsOnProductId, dependency.Strength,
            dependency.InteractionStyle.ToFlags(), dependency.Description, dependency.Period, actor, timestamp));

        return Result.Success(dependency);
    }

    private void Close(ProductDependency dependency, LocalDate endsOn, EventActor actor, Instant timestamp)
    {
        dependency.Period = new FlexibleDateRange(dependency.Period.Start, endsOn);

        AddDomainEvent(new ProductDependencyEndedEvent(
            Id, Key, dependency.Id, dependency.DependsOnProductId, dependency.Strength,
            dependency.InteractionStyle.ToFlags(), dependency.Period, actor, timestamp));
    }

    /// <summary>
    /// Updates the node's name or description.
    /// </summary>
    /// <remarks>
    /// Raises nothing when every value already matches, so an unedited save records no change. Compares
    /// after assignment because the setters trim.
    /// </remarks>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        var previous = (Name, Description);

        Name = name;
        Description = description;

        if ((Name, Description) == previous)
        {
            return Result.Success();
        }

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

        var previousExternalId = ExternalId;
        ExternalId = newExternalId;

        AddDomainEvent(new ProductLinkedExternallyEventV2(Id, Key, previousExternalId, ExternalId, actor, timestamp));

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
    /// <param name="dependsAcrossNewLineage">
    /// Whether an open dependency links this node, or anything beneath it, with the new parent or anything
    /// above it, in either direction. Supplied by the caller, which owns that query.
    /// </param>
    /// <remarks>
    /// <strong>The cycle check is only as good as <paramref name="ancestorIds"/>.</strong> Passing an
    /// empty collection for a non-null parent silently disables it; the domain cannot query the tree to
    /// notice. Only the self-parent case is caught unconditionally.
    /// <para>
    /// A move that would put two products depending on each other above and below one another is refused,
    /// for the reason <see cref="AddDependency"/> refuses such a link: that relationship is composition. An
    /// ended dependency does not block it, since it held while the two were apart.
    /// </para>
    /// </remarks>
    public Result Reparent(Guid? parentId, IReadOnlyCollection<Guid> ancestorIds, bool dependsAcrossNewLineage, EventActor actor, Instant timestamp)
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

        if (dependsAcrossNewLineage)
        {
            return Result.Failure(
                "This move would place a product above or below a product it has an open dependency with. End that dependency first.");
        }

        var fromParentId = ParentId;
        ParentId = parentId;

        AddDomainEvent(new ProductReparentedEventV2(Id, Key, fromParentId, parentId, actor, timestamp));

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

        AddDomainEvent(new ProductRetypedEventV2(Id, Key, fromProductTypeId, productTypeId, actor, timestamp));

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

        AddDomainEvent(new ProductLifecycleChangedEventV2(
            Id, Key,
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
    /// <param name="hasDependencies">Whether this node has any dependency recorded, ended ones included.</param>
    /// <param name="isDependedOn">
    /// Whether any other node has a dependency on this one recorded, ended ones included.
    /// </param>
    public Result Remove(bool hasChildren, bool hasVersions, bool isInAManifest, bool hasDependencies, bool isDependedOn, EventActor actor, Instant timestamp)
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

        // Ended links block too: what a product depended on, and when, is history that removing either end
        // would erase. A link recorded by mistake is removed first.
        if (hasDependencies)
        {
            return Result.Failure("This product has dependencies on other products recorded and cannot be removed.");
        }

        if (isDependedOn)
        {
            return Result.Failure("Other products have dependencies on this product recorded, so it cannot be removed.");
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

        // Deferred because Key is database-generated: an event raised here would carry Key 0. Every
        // other value is captured now rather than read when the action runs, so the event records the
        // product as created even where a caller — the import, tagging a new row — changes it first.
        var createdName = product.Name;
        var createdDescription = product.Description;
        var createdProductTypeId = product.ProductTypeId;
        var createdParentId = product.ParentId;
        var createdStatusId = product.StatusId;
        var createdStatusCategory = product.StatusCategory;

        product.AddPostPersistenceAction(() => product.AddDomainEvent(new ProductAddedEvent(
            product.Id,
            product.Key,
            createdName,
            createdDescription,
            createdProductTypeId,
            createdParentId,
            createdStatusId,
            createdStatusCategory,
            actor,
            timestamp)));

        return product;
    }
}
