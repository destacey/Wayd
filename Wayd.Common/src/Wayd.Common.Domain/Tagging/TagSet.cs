namespace Wayd.Common.Domain.Tagging;

/// <summary>
/// One tag applied to one record, as a tagged aggregate stores it.
/// </summary>
public interface ITagAssignment
{
    /// <summary>The tag carried.</summary>
    Guid TagId { get; }

    /// <summary>The tag's axis, denormalized so the one-tag-per-axis rule needs no load of the tag.</summary>
    Guid CategoryId { get; }
}

/// <summary>
/// What a tagging call changed: the fact, for a consumer that reacts to it, and the whole resulting set, for one
/// that keeps a copy.
/// </summary>
/// <param name="Added">Tags the record gained.</param>
/// <param name="Removed">Tags the record lost, including one a single-value axis replaced.</param>
/// <param name="TagIds">Every tag the record carries afterwards.</param>
public sealed record TagChange(Guid[] Added, Guid[] Removed, Guid[] TagIds);

/// <summary>
/// The set rules every tagged aggregate follows, applied to the assignment list it owns.
/// </summary>
/// <remarks>
/// Only the set mechanics live here. Whether the record may be tagged at all — an inactive tag, an axis a desk
/// does not offer — is the aggregate's to check first, and raising the event is the aggregate's, because each
/// module's event carries that module's record.
/// </remarks>
public static class TagSet
{
    /// <summary>
    /// Adds the tag to the set. On an axis that takes one tag, this replaces the tag the record held there rather
    /// than refusing, because choosing another value is a correction, not an error.
    /// </summary>
    /// <param name="assignments">The aggregate's own assignment list, which this changes.</param>
    /// <param name="tag">The tag to apply.</param>
    /// <param name="category">The tag's own axis; any other is a caller bug, so it throws.</param>
    /// <param name="createAssignment">Builds the aggregate's assignment from the tag id and axis id.</param>
    /// <returns>What changed, or null when the record already carried the tag.</returns>
    public static TagChange? Apply<TAssignment, TTag>(
        List<TAssignment> assignments,
        TTag tag,
        TagCategoryBase<TTag> category,
        Func<Guid, Guid, TAssignment> createAssignment)
        where TAssignment : ITagAssignment
        where TTag : TagBase
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(category);
        if (tag.CategoryId != category.Id)
            throw new ArgumentException("The tag must belong to the supplied axis.", nameof(category));

        if (assignments.Any(a => a.TagId == tag.Id))
            return null;

        Guid[] removed = [];
        if (!category.AllowsMany)
        {
            removed = [.. assignments.Where(a => a.CategoryId == category.Id).Select(a => a.TagId)];
            assignments.RemoveAll(a => a.CategoryId == category.Id);
        }

        assignments.Add(createAssignment(tag.Id, category.Id));

        return new TagChange([tag.Id], removed, TagIds(assignments));
    }

    /// <summary>
    /// Takes the tag out of the set, inactive ones included.
    /// </summary>
    /// <returns>What changed, or null when the record did not carry the tag.</returns>
    public static TagChange? Remove<TAssignment>(List<TAssignment> assignments, Guid tagId)
        where TAssignment : ITagAssignment
    {
        if (assignments.RemoveAll(a => a.TagId == tagId) == 0)
            return null;

        return new TagChange([], [tagId], TagIds(assignments));
    }

    private static Guid[] TagIds<TAssignment>(List<TAssignment> assignments) where TAssignment : ITagAssignment =>
        [.. assignments.Select(a => a.TagId)];
}
