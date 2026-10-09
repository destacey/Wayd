using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>An Organization holiday calendar's id, key and name, or null when there is no calendar with this id.</summary>
public sealed record GetHolidayCalendarNavigationQuery(Guid Id) : IQuery<NavigationDto?>;
