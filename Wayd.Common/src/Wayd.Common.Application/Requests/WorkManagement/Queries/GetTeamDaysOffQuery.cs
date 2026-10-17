using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Application.Requests.WorkManagement.Queries;

/// <summary>
/// The team days off of every team's sprints from <paramref name="From"/> to <paramref name="To"/>, both
/// inclusive, by team id, in date order: days a whole team is off for some or all of beyond its working week and
/// holidays, such as an offsite. A team with none in the range has no entry.
/// </summary>
/// <remarks>
/// One read however many teams and sprints, so a reader spanning many teams, such as a planning interval, asks
/// once. Days of a sprint outside the range are left out.
/// </remarks>
public sealed record GetTeamDaysOffQuery(LocalDate From, LocalDate To) : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<TeamDayOff>>>;
