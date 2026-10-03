using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.AppIntegration.Application.Connections.Queries;

/// <summary>
/// Retrieves activity history for a connection, newest first. Null when the connection does not exist or has
/// been deleted.
/// </summary>
public sealed record GetConnectionActivitiesQuery(Guid Id, int Page = 1, int PageSize = 50)
    : IQuery<Result<PagedResponse<ActivityLogDto>?>>;

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
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
