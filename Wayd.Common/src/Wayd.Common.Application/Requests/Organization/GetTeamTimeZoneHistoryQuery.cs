using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The time zone of every operating model a team or team of teams has had, in date order. Empty when there is
/// no team or team of teams with that id.
/// </summary>
/// <remarks>
/// For a report that counts days in the zone of the team it is for. <see cref="GetTeamScheduleHistoryQuery"/>
/// reads the rest of a team's operating model, and has nothing for a team of teams.
/// </remarks>
public sealed record GetTeamTimeZoneHistoryQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamTimeZonePeriod>>;

/// <summary>The time zone a team or team of teams counted its days in over a period.</summary>
/// <param name="Start">The first day of the operating model.</param>
/// <param name="End">Inclusive; null for the current operating model.</param>
/// <param name="TimeZone">The IANA id of the time zone.</param>
public sealed record TeamTimeZonePeriod(LocalDate Start, LocalDate? End, string TimeZone);
