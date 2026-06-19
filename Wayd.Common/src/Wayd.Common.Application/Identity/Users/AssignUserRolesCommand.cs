namespace Wayd.Common.Application.Identity.Users;

public sealed record AssignUserRolesCommand
{
    public AssignUserRolesCommand(string userId, List<string> roleNames)
    {
        UserId = userId;
        RoleNames = roleNames ?? [];
    }

    public string UserId { get; }
    public List<string> RoleNames { get; }
}