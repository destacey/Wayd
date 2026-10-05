using NodaTime;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The time zone and commitment grace period of every operating model an Organization team has had, in date
/// order. Empty when the team does not exist.
/// </summary>
/// <remarks>
/// For reading many sprints of one team at once; <see cref="GetTeamScheduleQuery"/> reads one day.
/// </remarks>
public sealed record GetTeamScheduleHistoryQuery(Guid TeamId) : IQuery<IReadOnlyList<TeamSchedulePeriodDto>>;

/// <summary>
/// <see cref="GetTeamScheduleHistoryQuery"/> for several teams in one read, keyed by team id. A team that does
/// not exist or has no operating models is missing from the result.
/// </summary>
public sealed record GetTeamsScheduleHistoryQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>>;

/// <param name="End">Inclusive; null for the current operating model.</param>
/// <param name="SizingMethod">Which of a work item's estimates the team's sprints in the period are measured in.</param>
public sealed record TeamSchedulePeriodDto(LocalDate Start, LocalDate? End, string TimeZone, int CommitmentGraceDays, SizingMethod SizingMethod);
