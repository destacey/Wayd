using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A swim lane slices a Story Map horizontally. Each lane holds tasks that belong together in
/// time — a milestone, a release, a "must have / nice to have" tier. Teams decide what a lane
/// means. Every map starts with a single default lane called "Tasks" that always stays at the top
/// and cannot be renamed, reordered, or removed.
/// </summary>
public sealed class SwimLane : BaseAuditableEntity
{
    /// <summary>
    /// The name of the default lane every map starts with.
    /// </summary>
    public const string DefaultLaneName = "Tasks";

    private SwimLane() { }

    internal SwimLane(Guid storyMapId, string name, int sortOrder, bool isDefault)
    {
        StoryMapId = storyMapId;
        Name = name;
        SortOrder = sortOrder;
        IsDefault = isDefault;
    }

    /// <summary>
    /// The Story Map this lane belongs to.
    /// </summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>
    /// The name of the lane, editable in place (except the default lane).
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The order of the lane on the map. The default lane is always at the top (order 0).
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Whether this is the default lane. New tasks land here, and it cannot be renamed, reordered,
    /// or removed.
    /// </summary>
    public bool IsDefault { get; private init; }

    /// <summary>
    /// An optional descriptive start date. Dates here are not validated and drive no behavior.
    /// </summary>
    public LocalDate? StartDate { get; private set; }

    /// <summary>
    /// An optional descriptive end date.
    /// </summary>
    public LocalDate? EndDate { get; private set; }

    internal Result Rename(string name)
    {
        if (IsDefault)
            return Result.Failure("The default lane cannot be renamed.");

        try
        {
            Name = name;
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    /// <summary>
    /// Sets the lane's descriptive dates. Both are optional; either or both may be cleared. The
    /// dates are descriptive only — no validation or ordering is enforced between them.
    /// </summary>
    internal void SetDates(LocalDate? startDate, LocalDate? endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Creates the default lane for a new map.
    /// </summary>
    internal static SwimLane CreateDefault(Guid storyMapId) => new(storyMapId, DefaultLaneName, 0, isDefault: true);
}
