using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The time zone, commitment grace period and sizing method of the operating model an Organization team had in effect on
/// <paramref name="AsOf"/>, or null when the team does not exist or had no operating model that day.
/// </summary>
/// <remarks>
/// A sprint reads its team's schedule as of its planned start date and keeps it for the whole sprint. Read it
/// each time rather than keeping a copy: correcting a model changes the schedule of every sprint in its period.
/// </remarks>
public sealed record GetTeamScheduleQuery(Guid TeamId, LocalDate AsOf) : IQuery<TeamScheduleDto?>;

/// <param name="SizingMethod">Which of a work item's estimates the team's work is measured in.</param>
public sealed record TeamScheduleDto(string TimeZone, int CommitmentGraceDays, SizingMethod SizingMethod);
