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
    /// Whether <see cref="EmployeeId"/> is part of this update. Only the admin user-edit path
    /// (Settings → Users) sets this; every other caller leaves it <c>false</c> and the link is left
    /// untouched.
    /// </summary>
    /// <remarks>
    /// The employee link is normally resolved server-side by email, not supplied by the caller —
    /// <c>GetEmployeeIdByEmail</c> at registration, <c>UpdateMissingEmployeeIds</c> as a backfill, and
    /// <c>SyncUsersFromEmployeeRecords</c> on people-sync. The admin picker is a manual override for
    /// when that resolution can't (mismatched email, external user); self-service profile editing has
    /// no employee field at all and must not disturb an authorization edge it does not own.
    /// <para>
    /// Hence the flag: without it, "caller isn't managing the link" and "caller wants the link
    /// cleared" are both <c>EmployeeId = null</c>, so the shared <see cref="IUserService.UpdateAsync"/>
    /// applied the default null on every profile save and silently unlinked the user — dropping their
    /// PPM role assignments, roadmap visibility, and team membership. Clearing the link is now an
    /// explicit act (<c>ManageEmployeeLink = true, EmployeeId = null</c>).
    /// </para>
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