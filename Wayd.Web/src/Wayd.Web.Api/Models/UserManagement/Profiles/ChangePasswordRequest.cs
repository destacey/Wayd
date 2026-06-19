namespace Wayd.Web.Api.Models.UserManagement.Profiles;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword)
{
    public ChangePasswordCommand ToChangePasswordCommand()
        => new(CurrentPassword, NewPassword);
}
