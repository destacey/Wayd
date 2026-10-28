using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// An axis reviewers file requests along — area, customer segment, review cycle — holding the tags that
/// belong to it.
/// </summary>
/// <remarks>
/// The reviewing team's own filing, distinct from the channel a request arrived through and from the themes
/// its requester picked: those are fixed vocabulary, while these axes change as the intake team learns what
/// it needs to slice by. Modelled on product tag axes, but owned by Demand Management; the two never share
/// rows.
/// <para>
/// Tags are reached only through their axis, because the rules a tag change can break — no two tags on one
/// axis sharing a name, a system axis staying as seeded — are the axis's to enforce.
/// </para>
/// </remarks>
public sealed class IntakeRequestTagCategory : BaseAuditableEntity
{
    private readonly List<IntakeRequestTag> _tags = [];

    private IntakeRequestTagCategory() { }

    private IntakeRequestTagCategory(string name, string? description, bool allowsMany, int order)
    {
        Name = name;
        Description = description;
        AllowsMany = allowsMany;
        Order = order;
    }

    /// <summary>What the company calls this axis.</summary>
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
    /// Whether a request can carry several tags from this axis. Fixed once the axis exists.
    /// </summary>
    /// <remarks>
    /// On a single-value axis, tagging a request replaces the tag it held rather than refusing, because
    /// choosing another value is a correction, not an error.
    /// </remarks>
    public bool AllowsMany { get; private init; }

    /// <summary>Position when presenting the axes, lowest first. Presentation only.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether the platform defines this axis. A system axis cannot be renamed, given tags or deleted; it can
    /// still be reordered and deactivated, which are the company's calls.
    /// </summary>
    /// <remarks>
    /// Reserved: no axis is a system axis yet. The seeded Area axis is the company's to change, so it is not one.
    /// </remarks>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Whether requests can still be tagged along this axis. Requests already tagged keep their tags.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The tags on this axis, in no particular order: a tag holds no position, so whoever presents them sorts
    /// them.
    /// </summary>
    public IReadOnlyCollection<IntakeRequestTag> Tags => _tags.AsReadOnly();

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

        AddDomainEvent(new IntakeRequestTagCategoryDetailsUpdatedEvent(Id, Name, Description, previousName,
            previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Moves the axis to another position in the list.
    /// </summary>
    public Result Reorder(int order, EventActor actor, Instant timestamp)
    {
        if (order == Order)
            return Result.Success();

        var fromOrder = Order;
        Order = order;

        AddDomainEvent(new IntakeRequestTagCategoryReorderedEvent(Id, fromOrder, Order, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes the axis out of use: nothing new is tagged along it, and requests already tagged keep their tags.
    /// </summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
            return Result.Success();

        IsActive = false;
        AddDomainEvent(new IntakeRequestTagCategoryDeactivatedEvent(Id, actor, timestamp));

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
        AddDomainEvent(new IntakeRequestTagCategoryActivatedEvent(Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the axis is being deleted, or refuses. Removing it, and its tags with it, is the caller's.
    /// </summary>
    /// <param name="isInUse">Whether any request carries a tag from this axis, which the axis cannot see.</param>
    public Result Delete(bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (isInUse)
            return Result.Failure(AxisInUseError);

        AddDomainEvent(new IntakeRequestTagCategoryDeletedEvent(Id, Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Adds a tag to the axis. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result<IntakeRequestTag> AddTag(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure<IntakeRequestTag>(SystemAxisError);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<IntakeRequestTag>("A tag must have a name.");

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: null))
            return Result.Failure<IntakeRequestTag>(DuplicateTagNameError);

        var tag = new IntakeRequestTag(Id, trimmed, description);
        _tags.Add(tag);

        AddDomainEvent(new IntakeRequestTagAddedEvent(Id, tag.Id, tag.Name, tag.Description, actor, timestamp));

        return tag;
    }

    /// <summary>
    /// Renames one of the axis's tags or changes its description. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// Requests refer to the tag by id, so every request carrying it reads the new name at once.
    /// </remarks>
    public Result UpdateTagDetails(Guid tagId, string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("A tag must have a name.");

        var trimmed = name.Trim();
        if (HasTagNamed(trimmed, exceptTagId: tagId))
            return Result.Failure(DuplicateTagNameError);

        var previousName = tag.Name;
        var previousDescription = tag.Description;

        tag.UpdateDetails(trimmed, description);

        if (tag.Name == previousName && tag.Description == previousDescription)
            return Result.Success();

        AddDomainEvent(new IntakeRequestTagDetailsUpdatedEvent(Id, tag.Id, tag.Name, tag.Description, previousName,
            previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Takes one of the axis's tags out of use. Requests already carrying it keep it and can still have it
    /// removed. Needs <see cref="Tags"/> loaded.
    /// </summary>
    public Result DeactivateTag(Guid tagId, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (!tag.IsActive)
            return Result.Success();

        tag.SetActive(false);
        AddDomainEvent(new IntakeRequestTagDeactivatedEvent(Id, tag.Id, actor, timestamp));

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
        AddDomainEvent(new IntakeRequestTagActivatedEvent(Id, tag.Id, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes one of the axis's tags outright. Needs <see cref="Tags"/> loaded.
    /// </summary>
    /// <remarks>
    /// A tag a request carries is deactivated instead, which stops new use without stripping it from the
    /// requests filed under it.
    /// </remarks>
    /// <param name="isInUse">Whether any request carries the tag, which the axis cannot see.</param>
    public Result RemoveTag(Guid tagId, bool isInUse, EventActor actor, Instant timestamp)
    {
        if (IsSystem)
            return Result.Failure(SystemAxisError);

        if (FindTag(tagId) is not { } tag)
            return Result.Failure(TagNotFoundError);

        if (isInUse)
            return Result.Failure(TagInUseError);

        _tags.Remove(tag);
        AddDomainEvent(new IntakeRequestTagRemovedEvent(Id, tag.Id, tag.Name, actor, timestamp));

        return Result.Success();
    }

    /// <summary>The failure for changing what a system axis means.</summary>
    public const string SystemAxisError = "System tag axes cannot be changed.";

    /// <summary>The failure for a tag id that is not on this axis.</summary>
    public const string TagNotFoundError = "That tag does not belong to this axis.";

    /// <summary>The failure for a tag name another tag on the axis already has.</summary>
    public const string DuplicateTagNameError = "A tag with this name already exists on this axis.";

    /// <summary>The failure for deleting a tag a request carries.</summary>
    public const string TagInUseError = "Requests carry this tag, so it cannot be deleted. Deactivate it instead.";

    /// <summary>The failure for deleting an axis requests are tagged along.</summary>
    public const string AxisInUseError = "Requests are tagged along this axis, so it cannot be deleted. Deactivate it instead.";

    private IntakeRequestTag? FindTag(Guid tagId) => _tags.FirstOrDefault(t => t.Id == tagId);

    private bool HasTagNamed(string name, Guid? exceptTagId) =>
        _tags.Any(t => t.Id != exceptTagId && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Defines an axis the company manages.
    /// </summary>
    public static IntakeRequestTagCategory Create(string name, string? description, bool allowsMany, int order,
        EventActor actor, Instant timestamp)
    {
        var category = new IntakeRequestTagCategory(name, description, allowsMany, order);

        category.AddDomainEvent(new IntakeRequestTagCategoryCreatedEvent(category.Id, category.Name,
            category.Description, category.AllowsMany, category.Order, actor, timestamp));

        return category;
    }
}
