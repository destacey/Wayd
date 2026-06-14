using Wayd.Common.Application.Identity.Roles;

namespace Wayd.Common.Application.Identity.Users;

public sealed record CreateUserCommand
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }
    public required string LoginProvider { get; set; }
    public string? Password { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public List<string> RoleNames { get; set; } = [];
}

public sealed class CreateUserCommandValidator : CustomValidator<CreateUserCommand>
{
    public CreateUserCommandValidator(IUserService userService, IRoleService roleService)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(u => u.Email)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.")
            .MustAsync(async (email, _) => !await userService.ExistsWithEmailAsync(email))
                .WithMessage((_, email) => string.Format("Email {0} is already registered.", email));

        RuleFor(u => u.PhoneNumber)
            .MustAsync(async (phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!))
                .WithMessage((_, phone) => string.Format("Phone number {0} is already registered.", phone))
                .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

        RuleFor(p => p.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(p => p.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(u => u.LoginProvider)
            .NotEmpty()
            .Must(lp => LoginProviders.All.Contains(lp))
                .WithMessage("Login provider must be one of: " + string.Join(", ", LoginProviders.All));

        RuleFor(u => u.Password)
            .NotEmpty()
                .WithMessage("Password is required for Wayd accounts.")
            .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]")
                .WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]")
                .WithMessage("Password must contain at least one digit.")
            .When(u => u.LoginProvider == LoginProviders.Wayd);

        RuleFor(u => u.Password)
            .Null()
                .WithMessage("Password must not be provided for non-Wayd accounts.")
            .When(u => u.LoginProvider != LoginProviders.Wayd);

        RuleFor(u => u.RoleNames)
            .NotEmpty()
                .WithMessage("At least one role must be assigned.");

        RuleForEach(u => u.RoleNames)
            .MustAsync(async (roleName, _) => await roleService.Exists(roleName, null))
                .WithMessage((_, roleName) => string.Format("Role {0} does not exist.", roleName))
            .When(u => u.RoleNames.Count > 0);
    }
}
