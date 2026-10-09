using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

public sealed class GetHolidayCalendarNavigationQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetHolidayCalendarNavigationQuery, NavigationDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<NavigationDto?> Handle(GetHolidayCalendarNavigationQuery request, CancellationToken cancellationToken) =>
        await _organizationDbContext.HolidayCalendars
            .Where(c => c.Id == request.Id)
            .Select(c => NavigationDto.Create(c.Id, c.Key, c.Name))
            .FirstOrDefaultAsync(cancellationToken);
}
