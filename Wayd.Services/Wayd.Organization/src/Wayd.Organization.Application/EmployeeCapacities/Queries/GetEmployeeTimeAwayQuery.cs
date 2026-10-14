using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// An employee's time away in date order, with the holiday calendar they take. Null when the employee does
/// not exist. The reason for each stretch is left out unless the caller may manage the employee's time away.
/// </summary>
public sealed record GetEmployeeTimeAwayQuery(Guid EmployeeId) : IQuery<EmployeeTimeAwayDto?>;

public sealed class GetEmployeeTimeAwayQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetEmployeeTimeAwayQuery, EmployeeTimeAwayDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<EmployeeTimeAwayDto?> Handle(GetEmployeeTimeAwayQuery request, CancellationToken cancellationToken)
    {
        var employeeId = request.EmployeeId;

        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == employeeId, cancellationToken))
            return null;

        var canManage = await _currentPrincipal.CanManageTimeAway(_organizationDbContext, employeeId, cancellationToken);

        var calendar = await (
            from capacity in _organizationDbContext.EmployeeCapacities
            where capacity.Id == employeeId
            join holidayCalendar in _organizationDbContext.HolidayCalendars on capacity.HolidayCalendarId equals holidayCalendar.Id
            select new { holidayCalendar.Id, holidayCalendar.Key, holidayCalendar.Name })
            .FirstOrDefaultAsync(cancellationToken);

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
            HolidayCalendar = calendar is null ? null : NavigationDto.Create(calendar.Id, calendar.Key, calendar.Name),
            Entries = entries,
        };
    }
}
