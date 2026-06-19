namespace Wayd.Common.Application.Identity.Users;

public sealed record ManageRoleUsersCommand
{
    public ManageRoleUsersCommand(string roleId, List<string> userIdsToAdd, List<string> userIdsToRemove)
    {
        RoleId = roleId;
        UserIdsToAdd = userIdsToAdd ?? [];
        UserIdsToRemove = userIdsToRemove ?? [];
    }

    public string RoleId { get; }
    public List<string> UserIdsToAdd { get; }
    public List<string> UserIdsToRemove { get; }
}
