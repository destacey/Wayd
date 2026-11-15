namespace Wayd.Common.Application.Identity.Users;

public sealed record UpdateUserCommand
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }

    /// <summary>
    /// Whether <see cref="EmployeeId"/> is part of this update. The admin user-edit path
    /// (Settings → Access → Users) sets this; a caller that leaves it <c>false</c> leaves the link untouched.
    /// </summary>
    /// <remarks>
    /// The employee link is normally resolved server-side by email, not supplied by the caller —
    /// <c>GetEmployeeIdByEmail</c> at registration and <c>UpdateMissingEmployeeIds</c> on people sync.
    /// The admin picker is a manual override for when that resolution can't (mismatched email, external
    /// user). Without the flag, "caller isn't managing the link" and "caller wants the link cleared" are
    /// both <c>EmployeeId = null</c>, and unlinking a user drops their PPM role assignments, roadmap
    /// visibility and team membership; clearing the link is an explicit act
    /// (<c>ManageEmployeeLink = true, EmployeeId = null</c>).
    /// </remarks>
    public bool ManageEmployeeLink { get; set; }
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

        // One user per employee. Previously enforced only by filtering the employee picker in the
        // admin edit-user form, so any caller bypassing that UI could link two users to one employee —
        // which silently doubles that employee's PPM role assignments and roadmap visibility. Skipped
        // when the update does not administer the link, and when clearing it (null is always allowed).
        RuleFor(u => u.EmployeeId)
            .MustAsync(async (user, employeeId, _) => !await userService.ExistsWithEmployeeIdAsync(employeeId!.Value, user.Id))
                .WithMessage("That employee is already linked to another user.")
                .When(u => u.ManageEmployeeLink && u.EmployeeId.HasValue);
    }
}