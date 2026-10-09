using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.Teams.Queries;

internal static class OperatingModelHolidayCalendars
{
    /// <summary>
    /// Sets each operating model DTO's holiday calendar from the model it was mapped from, in one read, so a caller
    /// who may view teams but not holiday calendars still sees which calendar a team uses.
    /// </summary>
    public static async Task<List<TeamOperatingModelDetailsDto>> WithHolidayCalendars(
        this IEnumerable<(TeamOperatingModelDetailsDto Dto, TeamOperatingModel Model)> mapped,
        IOrganizationDbContext organizationDbContext,
        CancellationToken cancellationToken)
    {
        var pairs = mapped.ToList();
        var ids = pairs.Where(p => p.Model.HolidayCalendarId.HasValue).Select(p => p.Model.HolidayCalendarId!.Value).Distinct().ToList();

        var calendars = ids.Count == 0
            ? []
            : await organizationDbContext.HolidayCalendars
                .Where(c => ids.Contains(c.Id))
                .Select(c => NavigationDto.Create(c.Id, c.Key, c.Name))
                .ToDictionaryAsync(c => c.Id, cancellationToken);

        foreach (var (dto, model) in pairs)
        {
            if (model.HolidayCalendarId is { } id)
                dto.HolidayCalendar = calendars.GetValueOrDefault(id);
        }

        return [.. pairs.Select(p => p.Dto)];
    }
}
