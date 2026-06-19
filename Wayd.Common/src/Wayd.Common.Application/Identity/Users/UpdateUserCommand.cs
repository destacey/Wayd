namespace Wayd.Common.Application.Identity.Users;

public sealed record UpdateUserCommand
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }
}

public sealed class UpdateUserCommandValidator : CustomValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator(IUserService userService)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Id)
            .NotEmpty();

        RuleFor(p => p.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(p => p.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(p => p.Email)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.")
            .MustAsync(async (user, email, _) => !await userService.ExistsWithEmailAsync(email, user.Id))
                .WithMessage((_, email) => string.Format("Email {0} is already registered.", email));

        RuleFor(u => u.PhoneNumber)
            .MustAsync(async (user, phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!, user.Id))
                .WithMessage((_, phone) => string.Format("Phone number {0} is already registered.", phone))
                .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));
    }
}