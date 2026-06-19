namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record ManageRoleUsersRequest
{
    public string RoleId { get; set; } = default!;
    public List<string> UserIdsToAdd { get; set; } = [];
    public List<string> UserIdsToRemove { get; set; } = [];

    public ManageRoleUsersCommand ToManageRoleUsersCommand()
        => new(RoleId, UserIdsToAdd, UserIdsToRemove);
}

public sealed class ManageRoleUsersRequestValidator : CustomValidator<ManageRoleUsersRequest>
{
    public ManageRoleUsersRequestValidator()
    {
        RuleFor(r => r.RoleId)
            .NotEmpty();
        RuleFor(r => r)
            .Must(r => (r.UserIdsToAdd != null && r.UserIdsToAdd.Count > 0) || (r.UserIdsToRemove != null && r.UserIdsToRemove.Count > 0))
            .WithMessage("At least one user must be added or removed.");
    }
}
