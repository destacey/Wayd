namespace Wayd.Web.Api.Models.Organizations.TeamMemberRoles;

/// <param name="Name">The role's name, unique across roles.</param>
/// <param name="Description">What the role is for.</param>
/// <param name="CountsTowardDeliveryCapacity">
/// Whether time allocated to a team in this role adds to the team's delivery capacity. Defaults to true; false
/// suits a role such as a manager's, whose time on the team does not burn its planned work.
/// </param>
public sealed record CreateTeamMemberRoleRequest(string Name, string? Description, bool? CountsTowardDeliveryCapacity = null);

/// <param name="Id">The role's id, which must match the route.</param>
/// <param name="Name">The role's name, unique across roles.</param>
/// <param name="Description">What the role is for.</param>
/// <param name="CountsTowardDeliveryCapacity">
/// Whether time allocated to a team in this role adds to the team's delivery capacity. Omit to leave it as it is.
/// </param>
public sealed record UpdateTeamMemberRoleRequest(Guid Id, string Name, string? Description, bool? CountsTowardDeliveryCapacity = null);
