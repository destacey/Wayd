using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.HolidayCalendars.Queries;

public sealed class HolidayCalendarExistsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<HolidayCalendarExistsQuery, bool>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<bool> Handle(HolidayCalendarExistsQuery request, CancellationToken cancellationToken) =>
        await _organizationDbContext.HolidayCalendars.AnyAsync(c => c.Id == request.Id, cancellationToken);
}
