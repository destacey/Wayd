using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Identity.Users.Queries;

/// <summary>
/// Retrieves activity history for a user account, newest first, including the activity of the personal access
/// tokens it owns. Null when the user does not exist.
/// </summary>
public sealed record GetUserActivitiesQuery(string UserId, int Page = 1, int PageSize = 50)
    : IQuery<Result<PagedResponse<ActivityLogDto>?>>;

public sealed class GetUserActivitiesQueryHandler(
    IWaydDbContext waydDbContext,
    IUserService userService,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetUserActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private const string PersonalAccessTokenType = "PersonalAccessToken";

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IUserService _userService = userService;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetUserActivitiesQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId)
            || await _userService.GetAsync(request.UserId, cancellationToken) is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            userId,
            aggregateType: "ApplicationUser",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(await ResolveRaisedOn(activities, cancellationToken));
    }

    /// <summary>
    /// Names the token each related entry was raised on. A token has no key, so the navigation carries 0; a
    /// deleted token is gone and stays unnamed, though its deletion entry carries the name.
    /// </summary>
    private async Task<PagedResponse<ActivityLogDto>> ResolveRaisedOn(
        PagedResponse<ActivityLogDto> activities, CancellationToken cancellationToken)
    {
        var tokenIds = activities.Items
            .Where(a => a.IsRelated && a.AggregateType == PersonalAccessTokenType)
            .Select(a => a.AggregateId)
            .Distinct()
            .ToList();

        if (tokenIds.Count == 0)
        {
            return activities;
        }

        var tokens = await _waydDbContext.PersonalAccessTokens
            .Where(t => tokenIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return activities with
        {
            Items = [.. activities.Items.Select(a =>
                a.IsRelated && a.AggregateType == PersonalAccessTokenType && tokens.TryGetValue(a.AggregateId, out var name)
                    ? a with { RaisedOn = NavigationDto.Create(a.AggregateId, 0, name) }
                    : a)],
        };
    }
}
