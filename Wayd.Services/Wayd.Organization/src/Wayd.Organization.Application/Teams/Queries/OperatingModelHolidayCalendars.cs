namespace Wayd.Organization.Application.Teams.Queries;

internal static class HolidayCalendarNames
{
    /// <summary>
    /// Fills in the name of each operating model's holiday calendar, in one read, so a caller who may view teams
    /// but not holiday calendars still sees which calendar a team uses.
    /// </summary>
    public static async Task<T> WithHolidayCalendarNames<T>(this T dtos, IOrganizationDbContext organizationDbContext, CancellationToken cancellationToken)
        where T : IEnumerable<TeamOperatingModelDetailsDto>
    {
        var ids = dtos.Where(d => d.HolidayCalendarId.HasValue).Select(d => d.HolidayCalendarId!.Value).Distinct().ToList();
        if (ids.Count == 0)
            return dtos;

        var names = await organizationDbContext.HolidayCalendars
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        foreach (var dto in dtos)
        {
            if (dto.HolidayCalendarId is { } id)
                dto.HolidayCalendarName = names.GetValueOrDefault(id);
        }

        return dtos;
    }
}
