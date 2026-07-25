using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A swim lane slices a Story Map horizontally. Each swim lane holds tasks that belong together in
/// time — a milestone, a release, a "must have / nice to have" tier. Teams decide what a swim lane
/// means. Every map starts with a single default swim lane called "Tasks" that always stays at the top
/// and cannot be renamed, reordered, or removed.
/// </summary>
public sealed class SwimLane : BaseAuditableEntity
{
    /// <summary>
    /// The name of the default swim lane every map starts with.
    /// </summary>
    public const string DefaultSwimLaneName = "Tasks";

    private SwimLane() { }

    internal SwimLane(Guid storyMapId, string name, int order, bool isDefault)
    {
        StoryMapId = storyMapId;
        Name = name;
        Order = order;
        IsDefault = isDefault;
    }

    /// <summary>
    /// The Story Map this swim lane belongs to.
    /// </summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>
    /// The name of the swim lane, editable in place (except the default swim lane).
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The order of the swim lane on the map. The default swim lane is always at the top (order 0).
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this is the default swim lane. New tasks land here, and it cannot be renamed, reordered,
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

    internal void SetOrder(int order) => Order = order;

    /// <summary>
    /// Sets the swim lane's descriptive dates. Both are optional; either or both may be cleared. The
    /// dates are descriptive only — no validation or ordering is enforced between them.
    /// </summary>
    internal void SetDates(LocalDate? startDate, LocalDate? endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// Creates the default swim lane for a new map.
    /// </summary>
    internal static SwimLane CreateDefault(Guid storyMapId) => new(storyMapId, DefaultSwimLaneName, 0, isDefault: true);
}
