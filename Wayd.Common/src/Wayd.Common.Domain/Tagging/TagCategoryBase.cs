using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// An axis a module's records are labelled along — Platform on products, Area on intake requests — holding the
/// tags that belong to it.
/// </summary>
/// <remarks>
/// Tags are grouped rather than kept in one flat list because different axes make different kinds of statement:
/// grouped, "what platform is this?" is a query rather than something the caller has to already know. The axes an
/// organization needs are not predictable, so they are data rather than an enum.
/// <para>
/// Tags are reached only through their axis, because the rules a tag change can break — no two tags on one axis
/// sharing a name, a system axis staying as seeded — are the axis's to enforce. Each module derives its own axis
/// with its own table and events; the rules are shared so every module tags the same way.
/// </para>
/// </remarks>
/// <typeparam name="TTag">The module's tag.</typeparam>
public abstract class TagCategoryBase<TTag> : BaseAuditableEntity where TTag : TagBase
{
    protected TagCategoryBase() { }

    protected TagCategoryBase(string name, string? description, bool allowsMany, int order, bool isSystem)
    {
        Name = name;
        Description = description;
        AllowsMany = allowsMany;
        Order = order;
        IsSystem = isSystem;
    }

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
    /// Whether a record can carry several tags from this axis. Fixed once the axis exists.
    /// </summary>
    /// <remarks>
    /// Fixed because narrowing an axis would leave records holding more tags than it permits, with no rule for
    /// choosing which to drop. On an axis that takes one tag, tagging a record replaces the tag it held rather than
    /// refusing — see <see cref="TagSet.Apply"/>.
    /// </remarks>
    public bool AllowsMany { get; private init; }

    /// <summary>Position when presenting the axes, lowest first. Presentation only.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether the platform seeded this axis. A system axis cannot be renamed, given tags or deleted, and its tags
    /// cannot be changed; it can still be reordered and deactivated, which are the organization's calls.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether records can still be tagged along this axis. Records already tagged keep their tags.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The tags on this axis, in no particular order: a tag holds no position, so whoever presents them sorts them.
    /// </summary>
    public IReadOnlyCollection<TTag> Tags => TagList.AsReadOnly();

    /// <summary>
    /// The derived axis's own tag list. Declared there rather than here so the persistence mapping finds the field
    /// on the mapped type.
    /// </summary>
    protected abstract List<TTag> TagList { get; }

    /// <summary>The failure for deleting a tag a record carries, naming the module's records.</summary>
    protected abstract string TagInUseMessage { get; }

    /// <summary>The failure for deleting an axis records are tagged along, naming the module's records.</summary>
    protected abstract string AxisInUseMessage { get; }

    /// <summary>
    /// Renames the axis or changes its description.
    /// </summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        var previousName = Name;
        var previousDescription = Description;

        Name = name;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        AddDomainEvent(DetailsUpdatedEvent(previousName, previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the axis to another position in the list.
    /// </summary>
    /// <remarks>
    /// Allowed on a system axis: where it sits among the others is the organization's call, and refusing it would
    /// pin every seeded axis above the organization's own for good.
    /// </remarks>
    public Result Reorder(int order, EventActor actor, Instant timestamp)
    {
        if (order == Order)
            return Result.Success();

        var fromOrder = Order;
        Order = order;

        AddDomainEvent(ReorderedEvent(fromOrder, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes the axis out of use: nothing new is tagged along it, and records already tagged keep their tags.
    /// </summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(DeactivatedEvent(actor, timestamp));

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
        AddDomainEvent(ActivatedEvent(actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the axis is being deleted, or refuses. Removing it, and its tags with it, is the caller's.
    /// </summary>
    /// <param name="isInUse">Whether any record carries a tag from this axis, which the axis cannot see.</param>
    public Result Delete(bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        if (isInUse)
            return Result.Failure(AxisInUseMessage);

        AddDomainEvent(DeletedEvent(actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Adds a tag to the axis. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result<TTag> AddTag(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure<TTag>(TagErrors.SystemAxis);

        return AddTagUnguarded(name, description, actor, timestamp);
    }

    /// <summary>
    /// Renames one of the axis's tags or changes its description. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// Records refer to the tag by id, so every record carrying it reads the new name at once.
    /// </remarks>
    public Result UpdateTagDetails(Guid tagId, string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagErrors.TagNotFound);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(TagErrors.TagNameRequired);

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: tagId))
            return Result.Failure(TagErrors.DuplicateTagName);

        var previousName = tag.Name;
        var previousDescription = tag.Description;

        tag.UpdateDetails(trimmed, description);

        if (tag.Name == previousName && tag.Description == previousDescription)
            return Result.Success();

        AddDomainEvent(TagDetailsUpdatedEvent(tag, previousName, previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes one of the axis's tags out of use. Records already carrying it keep it and can still have it removed.
    /// Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// Refused on a system axis: a seeded tag deactivated would leave records untaggable with it and no way to
    /// restore it, because seeders do not re-add it.
    /// </remarks>
    public Result DeactivateTag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagErrors.TagNotFound);

        if (!tag.IsActive)
            return Result.Success();

        tag.SetActive(false);
        AddDomainEvent(TagDeactivatedEvent(tag, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Puts one of the axis's tags back into use. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result ActivateTag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagErrors.TagNotFound);

        if (tag.IsActive)
            return Result.Success();

        tag.SetActive(true);
        AddDomainEvent(TagActivatedEvent(tag, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes one of the axis's tags outright. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// A tag a record carries is deactivated instead, which stops new use without stripping it from the records
    /// labelled with it.
    /// </remarks>
    /// <param name="isInUse">Whether any record carries the tag, which the axis cannot see.</param>
    public Result RemoveTag(Guid tagId, bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(TagErrors.SystemAxis);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagErrors.TagNotFound);

        if (isInUse)
            return Result.Failure(TagInUseMessage);

        TagList.Remove(tag);
        AddDomainEvent(TagRemovedEvent(tag, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Adds a tag without the system-axis guard. For a module's seeder building its seeded axis.
    /// </summary>
    protected Result<TTag> AddTagUnguarded(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<TTag>(TagErrors.TagNameRequired);

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: null))
            return Result.Failure<TTag>(TagErrors.DuplicateTagName);

        var tag = CreateTag(trimmed, description);
        TagList.Add(tag);

        AddDomainEvent(TagAddedEvent(tag, actor, timestamp));

        return tag;
    }

    /// <summary>
    /// Records the axis's creation. Called once by the derived axis's factory, after construction.
    /// </summary>
    protected void RaiseCreated(EventActor actor, Instant timestamp) =>
        AddDomainEvent(CreatedEvent(actor, timestamp));

    /// <summary>Builds a new tag on this axis.</summary>
    protected abstract TTag CreateTag(string name, string? description);

    /// <summary>The module's event for the axis being created.</summary>
    protected abstract DomainEvent CreatedEvent(EventActor actor, Instant timestamp);

    /// <summary>The module's event for the axis being renamed or re-described; the current values are on the axis.</summary>
    protected abstract DomainEvent DetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp);

    /// <summary>The module's event for the axis moving; the new position is on the axis.</summary>
    protected abstract DomainEvent ReorderedEvent(int fromOrder, EventActor actor, Instant timestamp);

    /// <summary>The module's event for the axis being put back into use.</summary>
    protected abstract DomainEvent ActivatedEvent(EventActor actor, Instant timestamp);

    /// <summary>The module's event for the axis being taken out of use.</summary>
    protected abstract DomainEvent DeactivatedEvent(EventActor actor, Instant timestamp);

    /// <summary>The module's event for the axis being deleted.</summary>
    protected abstract DomainEvent DeletedEvent(EventActor actor, Instant timestamp);

    /// <summary>The module's event for a tag being added to the axis.</summary>
    protected abstract DomainEvent TagAddedEvent(TTag tag, EventActor actor, Instant timestamp);

    /// <summary>The module's event for a tag being renamed or re-described; the current values are on the tag.</summary>
    protected abstract DomainEvent TagDetailsUpdatedEvent(TTag tag, string previousName, string? previousDescription, EventActor actor, Instant timestamp);

    /// <summary>The module's event for a tag being put back into use.</summary>
    protected abstract DomainEvent TagActivatedEvent(TTag tag, EventActor actor, Instant timestamp);

    /// <summary>The module's event for a tag being taken out of use.</summary>
    protected abstract DomainEvent TagDeactivatedEvent(TTag tag, EventActor actor, Instant timestamp);

    /// <summary>The module's event for a tag being removed from the axis.</summary>
    protected abstract DomainEvent TagRemovedEvent(TTag tag, EventActor actor, Instant timestamp);

    private TTag? FindTag(Guid tagId) => TagList.FirstOrDefault(t => t.Id == tagId);

    private bool HasTagNamed(string name, Guid? exceptTagId) =>
        TagList.Any(t => t.Id != exceptTagId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
