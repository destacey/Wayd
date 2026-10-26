using Wayd.Common.Application.Activities.Dtos;

namespace Wayd.Common.Application.Activities;

/// <summary>
/// Reads the business activity log of one tracked aggregate.
/// </summary>
/// <remarks>
/// A service rather than a standalone query so a domain module's own query handler can resolve and
/// authorize its record first, and then read the activity history within that record's security boundary.
/// </remarks>
public interface IActivityLogReader : IScopedService
{
    /// <summary>
    /// Reads one page of an aggregate's activity entries, newest first.
    /// </summary>
    /// <param name="aggregateId">The aggregate root identifier.</param>
    /// <param name="aggregateType">The aggregate type name to filter by; null for entries of any type.</param>
    /// <param name="paging">The page to read, already validated by the calling query; null for the first page at the default size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResponse<ActivityLogDto>> Read(
        Guid aggregateId,
        string? aggregateType = null,
        PageRequest? paging = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of the aggregate's activity entries of the given event types only, newest first.
    /// </summary>
    /// <remarks>
    /// For a record whose history is read in tiers: the filter runs in the query, so the page and the total
    /// count only ever cover what the reader may see. Filtering a page afterwards would show short pages and
    /// a total that discloses how many entries were withheld.
    /// </remarks>
    /// <param name="aggregateId">The aggregate root identifier.</param>
    /// <param name="aggregateType">The aggregate type name.</param>
    /// <param name="eventTypes">The event type names the reader may see.</param>
    /// <param name="paging">The page to read, already validated by the calling query; null for the first page at the default size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResponse<ActivityLogDto>> ReadEventTypes(
        Guid aggregateId,
        string aggregateType,
        IReadOnlyCollection<string> eventTypes,
        PageRequest? paging = null,
        CancellationToken cancellationToken = default);
}
