using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Which company tag categories an intake desk offers its agents changed: it switched between offering every category and
/// only those it chose, or it chose different categories.
/// </summary>
/// <remarks>
/// A desk offering every category also offers categories added later, so its chosen list is empty. A desk offering only its
/// chosen categories with none chosen offers none, and its requests are not tagged.
/// </remarks>
public sealed record IntakeDeskTagCategoriesChangedEvent : DomainEvent<IntakeDeskTagCategoriesChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskTagCategoriesChangedEvent(
        Guid id,
        int key,
        bool offersAllTagCategories,
        bool previousOffersAllTagCategories,
        IReadOnlyList<Guid> addedTagCategoryIds,
        IReadOnlyList<Guid> removedTagCategoryIds,
        IReadOnlyList<Guid> tagCategoryIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OffersAllTagCategories = offersAllTagCategories;
        PreviousOffersAllTagCategories = previousOffersAllTagCategories;
        AddedTagCategoryIds = addedTagCategoryIds;
        RemovedTagCategoryIds = removedTagCategoryIds;
        TagCategoryIds = tagCategoryIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>Whether the desk now offers every active tag category, including ones added later.</summary>
    public bool OffersAllTagCategories { get; }

    /// <summary>Whether it did before the change.</summary>
    public bool PreviousOffersAllTagCategories { get; }

    /// <summary>The categories newly chosen.</summary>
    public IReadOnlyList<Guid> AddedTagCategoryIds { get; }

    /// <summary>The categories no longer chosen.</summary>
    public IReadOnlyList<Guid> RemovedTagCategoryIds { get; }

    /// <summary>The categories chosen after the change; empty when the desk offers every category.</summary>
    public IReadOnlyList<Guid> TagCategoryIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
