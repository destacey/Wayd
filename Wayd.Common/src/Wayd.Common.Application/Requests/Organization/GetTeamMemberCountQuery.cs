namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The number of distinct employees on an Organization team, whatever their roles, or null when
/// the team does not exist.
/// </summary>
public sealed record GetTeamMemberCountQuery(Guid TeamId) : IQuery<int?>;
