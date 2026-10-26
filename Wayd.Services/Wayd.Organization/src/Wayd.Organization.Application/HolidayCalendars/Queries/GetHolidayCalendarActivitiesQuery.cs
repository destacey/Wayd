using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

/// <summary>
/// A holiday calendar's activity history, newest first; null when there is no such calendar.
/// </summary>
public sealed record GetHolidayCalendarActivitiesQuery : IQuery<PagedResponse<ActivityLogDto>?>, IPagedQuery
{
    public GetHolidayCalendarActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<HolidayCalendar>();
        Paging = paging;
    }

    public Expression<Func<HolidayCalendar, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetHolidayCalendarActivitiesQueryValidator : PagedQueryValidator<GetHolidayCalendarActivitiesQuery>;

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
            paging: request.Paging,
            cancellationToken: cancellationToken);
    }
}
