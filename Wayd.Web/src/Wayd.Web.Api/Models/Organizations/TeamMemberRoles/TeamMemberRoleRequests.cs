namespace Wayd.Web.Api.Models.Organizations.TeamMemberRoles;

public sealed record CreateTeamMemberRoleRequest(string Name, string? Description);

public sealed record UpdateTeamMemberRoleRequest(Guid Id, string Name, string? Description);
