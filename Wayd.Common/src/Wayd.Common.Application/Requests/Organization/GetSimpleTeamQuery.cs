using Wayd.Common.Domain.Interfaces.Organization;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The current state of one Organization team, or null when it no longer exists. Read by the modules that
/// keep a copy of a team, when an event reaches them for a team they have no copy of.
/// </summary>
public sealed record GetSimpleTeamQuery(Guid Id) : IQuery<ISimpleTeam?>;
