using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// One label a module's records can carry — <c>ios</c> on a product's Platform axis, "Payments" on an intake
/// request's Area axis, or a free tag such as "migration wave 2" on no axis at all.
/// </summary>
/// <remarks>
/// A row rather than text on the record, so a rename happens once, a picker lists the choices, and "everything
/// tagged ios" is a join. Descriptive only: nothing in the domain branches on a tag. Each module derives its own
/// tag, with its own table; modules never share tags.
/// <para>
/// A tag on an axis is part of that axis and changes only through its <see cref="TagCategoryBase{TTag}"/>. A free
/// tag has no axis, so it is its own aggregate: anyone who can tag a record can create one by typing its name, and
/// an admin renames, merges or deletes it. Free tags are never deactivated — one nothing carries is deleted.
/// </para>
/// </remarks>
public abstract partial class TagBase : BaseAuditableEntity
{
    /// <summary>The longest name a tag can have.</summary>
    public const int NameMaxLength = 64;

    protected TagBase() { }

    /// <summary>A tag on an axis.</summary>
    protected TagBase(Guid categoryId, string name, string? description)
    {
        CategoryId = Guard.Against.Default(categoryId, nameof(categoryId));
        Name = name;
        Description = description;
    }

    /// <summary>A free tag, already validated by <see cref="ValidateFreeName"/>.</summary>
    protected TagBase(string normalizedName)
    {
        Name = normalizedName;
    }

    /// <summary>The axis this tag belongs to, or null for a free tag.</summary>
    public Guid? CategoryId { get; private init; }

    /// <summary>Whether this is a free tag: one on no axis, created by typing its name while tagging.</summary>
    public bool IsFree => CategoryId is null;

    /// <summary>
    /// The label. Unique within its axis, or among the module's free tags, ignoring case.
    /// </summary>
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
    /// Whether records can still be given this tag. Records already carrying it keep it. Always true for a free tag.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Renames a free tag or changes its description. The caller checks that no other free tag has the name, which
    /// the tag cannot see; the database refuses a duplicate regardless.
    /// </summary>
    /// <remarks>
    /// Records refer to the tag by id, so every record carrying it reads the new name at once.
    /// </remarks>
    public Result UpdateFreeTagDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (!IsFree)
            return Result.Failure(TagErrors.NotAFreeTag);

        var validated = ValidateFreeName(name);
        if (validated.IsFailure)
            return validated;

        var previousName = Name;
        var previousDescription = Description;

        Name = validated.Value;
        Description = description;

        if (Name == previousName && Description == previousDescription)
            return Result.Success();

        AddDomainEvent(FreeTagDetailsUpdatedEvent(previousName, previousDescription, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that a free tag is being deleted, or refuses. Removing the row is the caller's, once nothing carries
    /// the tag.
    /// </summary>
    /// <param name="mergedIntoTagId">The tag its records were moved to, when the deletion completes a merge.</param>
    public Result DeleteFreeTag(Guid? mergedIntoTagId, EventActor actor, Instant timestamp)
    {
        if (!IsFree)
            return Result.Failure(TagErrors.NotAFreeTag);

        if (mergedIntoTagId == Id)
            return Result.Failure(TagErrors.MergeIntoItself);

        AddDomainEvent(FreeTagDeletedEvent(mergedIntoTagId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// The form a typed free tag name is stored and compared in: trimmed, with each run of whitespace inside it
    /// collapsed to one space, so "migration  wave" and "migration wave" are one tag.
    /// </summary>
    public static Result<string> ValidateFreeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<string>(TagErrors.TagNameRequired);

        var normalized = Whitespace().Replace(name.Trim(), " ");
        if (normalized.Length > NameMaxLength)
            return Result.Failure<string>(TagErrors.TagNameTooLong);

        return normalized;
    }

    /// <summary>
    /// Records a free tag's creation. Called once by the derived tag's factory, after construction.
    /// </summary>
    protected void RaiseFreeTagCreated(EventActor actor, Instant timestamp) =>
        AddDomainEvent(FreeTagCreatedEvent(actor, timestamp));

    /// <summary>The module's event for a free tag being created.</summary>
    protected abstract DomainEvent FreeTagCreatedEvent(EventActor actor, Instant timestamp);

    /// <summary>The module's event for a free tag being renamed or re-described; the current values are on the tag.</summary>
    protected abstract DomainEvent FreeTagDetailsUpdatedEvent(string previousName, string? previousDescription, EventActor actor, Instant timestamp);

    /// <summary>The module's event for a free tag being deleted.</summary>
    protected abstract DomainEvent FreeTagDeletedEvent(Guid? mergedIntoTagId, EventActor actor, Instant timestamp);

    internal void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    internal void SetActive(bool isActive) => IsActive = isActive;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
