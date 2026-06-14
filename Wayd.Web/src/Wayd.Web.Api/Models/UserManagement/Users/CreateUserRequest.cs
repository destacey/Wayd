namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record CreateUserRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }
    public required string LoginProvider { get; set; }
    public string? Password { get; set; }
    public List<string> RoleNames { get; set; } = [];

    public CreateUserCommand ToCreateUserCommand()
        => new()
        {
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            PhoneNumber = PhoneNumber,
            EmployeeId = EmployeeId,
            LoginProvider = LoginProvider,
            Password = Password,
            RoleNames = RoleNames
        };
}
