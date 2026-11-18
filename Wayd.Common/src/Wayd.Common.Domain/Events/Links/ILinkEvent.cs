using Wayd.Common.Domain.Links;

namespace Wayd.Common.Domain.Events.Links;

/// <summary>
/// Marks every event raised by a link, and names the record the link is on.
/// </summary>
/// <remarks>
/// A link's activity belongs on its owner as well as on the link, so every link event relates the owner through
/// <see cref="IRelatedAggregateEvent"/>, filed under the activity type its owner type declares. A module's handlers
/// react only to links on their own records, so they filter on <see cref="OwnerType"/> without loading the link.
/// </remarks>
public interface ILinkEvent : IEvent, IRelatedAggregateEvent
{
    /// <summary>The registered owner type of the link, such as <c>ppm.project</c>.</summary>
    string OwnerType { get; }

    /// <summary>The record the link is on.</summary>
    Guid OwnerId { get; }
}

/// <summary>Works out the records a link event is filed under besides the link.</summary>
internal static class LinkEventRelations
{
    /// <summary>
    /// The owner, by the activity type its owner type declares, or nothing when the owner type is no longer
    /// registered.
    /// </summary>
    public static IReadOnlyCollection<AggregateReference> For(string ownerType, Guid ownerId)
    {
        var descriptor = LinkOwnerTypes.Resolve(ownerType);
        return descriptor.IsSuccess
            ? [new AggregateReference(descriptor.Value.ActivityAggregateType, ownerId)]
            : [];
    }
}
