namespace Wayd.Web.Api.Models.Organizations.Teams;

public sealed record AddTeamMemberRequest(Guid EmployeeId, IReadOnlyList<Guid> RoleIds);

public sealed record UpdateTeamMemberRequest(IReadOnlyList<Guid> RoleIds);
