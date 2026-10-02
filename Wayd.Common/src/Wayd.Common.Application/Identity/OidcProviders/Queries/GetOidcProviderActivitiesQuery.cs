using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.OidcProviders.Queries;

/// <summary>
/// Retrieves activity history for an OIDC provider, newest first. Null when the provider does not exist.
/// </summary>
public sealed record GetOidcProviderActivitiesQuery(Guid Id, int Page = 1, int PageSize = 50)
    : IQuery<Result<PagedResponse<ActivityLogDto>?>>;

public sealed class GetOidcProviderActivitiesQueryHandler(
    IWaydDbContext waydDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetOidcProviderActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetOidcProviderActivitiesQuery request, CancellationToken cancellationToken)
    {
        var exists = await _waydDbContext.OidcProviders
            .AnyAsync(p => p.Id == request.Id, cancellationToken);

        if (!exists)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            request.Id,
            aggregateType: "OidcProvider",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
