using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.Tagging;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// A category reviewers group requests by — area, customer segment, review cycle — holding the tags that
/// belong to it.
/// </summary>
/// <remarks>
/// The reviewing team's own filing, distinct from the channel a request arrived through and from the themes
/// its requester picked: those are fixed vocabulary, while these categories change as the intake team learns
/// what it needs to slice by. The rules are shared with every module's tag categories; see
/// <see cref="TagCategoryBase{TTag}"/>.
/// </remarks>
public sealed class IntakeRequestTagCategory : TagCategoryBase<IntakeRequestTag>
{
    private readonly List<IntakeRequestTag> _tags = [];

    private IntakeRequestTagCategory() { }

    private IntakeRequestTagCategory(string name, string? description, bool allowsMany, int order)
        : base(name, description, allowsMany, order, isSystem: false)
    {
    }

    /// <summary>The failure for changing what a system category means.</summary>
    /// <remarks>
    /// No category is a system category yet. The seeded Area category is the company's to change, so it is not one.
    /// </remarks>
    public const string SystemCategoryError = TagErrors.SystemCategory;

    /// <summary>The failure for a tag id that is not in this category.</summary>
    public const string TagNotFoundError = TagErrors.TagNotFound;

    /// <summary>The failure for a tag with a blank name.</summary>
    public const string TagNameRequiredError = TagErrors.TagNameRequired;

    /// <summary>The failure for a tag name another tag in the category already has.</summary>
    public const string DuplicateTagNameError = TagErrors.DuplicateTagName;

    /// <summary>The failure for deleting a tag a request carries.</summary>
    public const string TagInUseError = "Requests carry this tag, so it cannot be deleted. Deactivate it instead.";

    /// <summary>The failure for deleting a category requests are tagged in.</summary>
    public const string CategoryInUseError = "Requests are tagged in this category, so it cannot be deleted. Deactivate it instead.";

    /// <summary>
    /// Defines a category the company manages.
    /// </summary>
    public static IntakeRequestTagCategory Create(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp)
    {
        var category = new IntakeRequestTagCategory(name, description, allowsMany, order);
        category.RaiseCreated(actor, timestamp);

        return category;
    }

    protected override List<IntakeRequestTag> TagList => _tags;

    protected override string TagInUseMessage => TagInUseError;

    protected override string CategoryInUseMessage => CategoryInUseError;

    protected override IntakeRequestTag CreateTag(string name, string? description) => new(Id, name, description);

    protected override DomainEvent CreatedEvent(EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryCreatedEvent(Id, Name, Description, AllowsMany, Order, actor, timestamp);

    protected override DomainEvent DetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryDetailsUpdatedEvent(Id, Name, Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent ReorderedEvent(int fromOrder, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryReorderedEvent(Id, fromOrder, Order, actor, timestamp);

    protected override DomainEvent ActivatedEvent(EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryActivatedEvent(Id, actor, timestamp);

    protected override DomainEvent DeactivatedEvent(EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryDeactivatedEvent(Id, actor, timestamp);

    protected override DomainEvent DeletedEvent(EventActor actor, Instant timestamp) =>
        new IntakeRequestTagCategoryDeletedEvent(Id, Name, actor, timestamp);

    protected override DomainEvent TagAddedEvent(IntakeRequestTag tag, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagAddedEvent(Id, tag.Id, tag.Name, tag.Description, actor, timestamp);

    protected override DomainEvent TagDetailsUpdatedEvent(IntakeRequestTag tag, string previousName, string? previousDescription, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagDetailsUpdatedEvent(Id, tag.Id, tag.Name, tag.Description, previousName, previousDescription, actor, timestamp);

    protected override DomainEvent TagActivatedEvent(IntakeRequestTag tag, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagActivatedEvent(Id, tag.Id, actor, timestamp);

    protected override DomainEvent TagDeactivatedEvent(IntakeRequestTag tag, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagDeactivatedEvent(Id, tag.Id, actor, timestamp);

    protected override DomainEvent TagRemovedEvent(IntakeRequestTag tag, EventActor actor, Instant timestamp) =>
        new IntakeRequestTagRemovedEvent(Id, tag.Id, tag.Name, actor, timestamp);
}
