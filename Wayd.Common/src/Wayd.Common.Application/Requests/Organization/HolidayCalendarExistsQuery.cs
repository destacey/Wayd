namespace Wayd.Common.Application.Requests.Organization;

/// <summary>Whether an Organization holiday calendar with this id exists.</summary>
public sealed record HolidayCalendarExistsQuery(Guid Id) : IQuery<bool>;
