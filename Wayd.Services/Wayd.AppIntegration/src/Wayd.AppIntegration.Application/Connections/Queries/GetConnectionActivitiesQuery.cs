using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.AppIntegration.Application.Connections.Queries;

/// <summary>
/// Retrieves activity history for a connection, newest first. Null when the connection does not exist or has
/// been deleted.
/// </summary>
public sealed record GetConnectionActivitiesQuery(Guid Id, PageRequest Paging)
    : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetConnectionActivitiesQueryValidator : PagedQueryValidator<GetConnectionActivitiesQuery>;

public sealed class GetConnectionActivitiesQueryHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetConnectionActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetConnectionActivitiesQuery request, CancellationToken cancellationToken)
    {
        var exists = await _appIntegrationDbContext.Connections
            .AnyAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (!exists)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            request.Id,
            aggregateType: "Connection",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
