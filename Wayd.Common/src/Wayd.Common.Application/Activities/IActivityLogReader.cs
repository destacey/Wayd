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
    /// Reads activity entries for an aggregate, newest first, with pagination.
    /// </summary>
    /// <param name="aggregateId">The aggregate root identifier.</param>
    /// <param name="aggregateType">Optional aggregate type name to filter by.</param>
    /// <param name="page">1-indexed page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResponse<ActivityLogDto>> Read(
        Guid aggregateId,
        string? aggregateType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
