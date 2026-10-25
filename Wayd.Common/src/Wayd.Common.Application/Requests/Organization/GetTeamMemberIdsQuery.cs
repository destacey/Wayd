namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The employees who are direct members of an Organization team now, in any role, each listed once. Members
/// of the teams it belongs to or contains are not included. Empty when the team does not exist.
/// </summary>
/// <remarks>
/// Includes inactive employees: the caller decides whether they count.
/// </remarks>
public sealed record GetTeamMemberIdsQuery(Guid TeamId) : IQuery<IReadOnlyList<Guid>>;
