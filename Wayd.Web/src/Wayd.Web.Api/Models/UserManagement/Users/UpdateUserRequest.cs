namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record UpdateUserRequest
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }

    public UpdateUserCommand ToUpdateUserCommand()
        => new()
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            PhoneNumber = PhoneNumber,
            EmployeeId = EmployeeId,
            // The admin user-edit form owns the employee link, so it opts in to applying it —
            // including clearing it (EmployeeId = null unlinks). The self-service profile edit does not.
            ManageEmployeeLink = true
        };
}
