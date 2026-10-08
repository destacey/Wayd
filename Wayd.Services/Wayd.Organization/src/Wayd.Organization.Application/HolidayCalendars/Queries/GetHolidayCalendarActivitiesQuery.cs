using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

/// <summary>
/// A holiday calendar's activity history, newest first; null when there is no such calendar.
/// </summary>
public sealed record GetHolidayCalendarActivitiesQuery : IQuery<PagedResponse<ActivityLogDto>?>
{
    public GetHolidayCalendarActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<HolidayCalendar>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<HolidayCalendar, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetHolidayCalendarActivitiesQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetHolidayCalendarActivitiesQuery, PagedResponse<ActivityLogDto>?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<PagedResponse<ActivityLogDto>?> Handle(GetHolidayCalendarActivitiesQuery request, CancellationToken cancellationToken)
    {
        var calendarId = await _organizationDbContext.HolidayCalendars
            .Where(request.IdOrKeyFilter)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (calendarId is null)
            return null;

        return await _activityLogReader.Read(
            calendarId.Value,
            aggregateType: "HolidayCalendar",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);
    }
}
