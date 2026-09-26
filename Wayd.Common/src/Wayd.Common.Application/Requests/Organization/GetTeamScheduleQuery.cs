using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The time zone and commitment grace period of the operating model an Organization team had in effect on
/// <paramref name="AsOf"/>, or null when the team does not exist or had no operating model that day.
/// </summary>
/// <remarks>
/// A sprint reads its team's schedule as of its planned start date and keeps it for the whole sprint. Read it
/// each time rather than keeping a copy: correcting a model changes the schedule of every sprint in its period.
/// </remarks>
public sealed record GetTeamScheduleQuery(Guid TeamId, LocalDate AsOf) : IQuery<TeamScheduleDto?>;

public sealed record TeamScheduleDto(string TimeZone, int CommitmentGraceDays);
