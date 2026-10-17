using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// An employee's time away in date order, with the holiday calendar they take and their working day. Null when
/// the employee does not exist. The reason for each stretch is left out unless the caller may manage the
/// employee's time away.
/// </summary>
public sealed record GetEmployeeTimeAwayQuery(Guid EmployeeId) : IQuery<EmployeeTimeAwayDto?>;

public sealed class GetEmployeeTimeAwayQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentPrincipal currentPrincipal,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetEmployeeTimeAwayQuery, EmployeeTimeAwayDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<EmployeeTimeAwayDto?> Handle(GetEmployeeTimeAwayQuery request, CancellationToken cancellationToken)
    {
        var employeeId = request.EmployeeId;

        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
            return null;

        var canManage = await _currentPrincipal.CanManageTimeAway(_organizationDbContext, employeeId, cancellationToken);

        var own = await (
            from capacity in _organizationDbContext.EmployeeCapacities
            where capacity.Id == employeeId
            join holidayCalendar in _organizationDbContext.HolidayCalendars on capacity.HolidayCalendarId equals holidayCalendar.Id into calendars
            from holidayCalendar in calendars.DefaultIfEmpty()
            select new
            {
                Calendar = holidayCalendar == null ? null : NavigationDto.Create(holidayCalendar.Id, holidayCalendar.Key, holidayCalendar.Name),
                capacity.WorkingDayHours,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ownWorkingDayHours = own?.WorkingDayHours;
        var systemWorkingDayHours = (await _schedulingSettings.Get(cancellationToken)).WorkingDayHours;

        var entries = await _organizationDbContext.EmployeeTimeAway
            .Where(t => t.EmployeeId == employeeId)
            .OrderBy(t => t.Start)
            .ThenBy(t => t.End)
            .Select(t => new EmployeeTimeAwayEntryDto
            {
                Id = t.Id,
                Start = t.Start,
                End = t.End,
                PercentAway = t.PercentAway,
                // Decided in the query, so a reason the caller is not shown never leaves the database.
                Reason = canManage ? t.Reason : (TimeAwayReason?)null,
            })
            .ToListAsync(cancellationToken);

        return new EmployeeTimeAwayDto
        {
            CanManage = canManage,
            WorkingDayHours = ownWorkingDayHours ?? systemWorkingDayHours,
            OwnWorkingDayHours = ownWorkingDayHours,
            SystemWorkingDayHours = systemWorkingDayHours,
            HolidayCalendar = own?.Calendar,
            Entries = entries,
        };
    }
}
