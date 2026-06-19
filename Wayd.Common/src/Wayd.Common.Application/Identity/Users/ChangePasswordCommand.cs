namespace Wayd.Common.Application.Identity.Users;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordCommandValidator : CustomValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(p => p.CurrentPassword)
            .NotEmpty();

        RuleFor(p => p.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("New password must be at least 8 characters.")
            .Must((cmd, newPassword) => newPassword != cmd.CurrentPassword)
                .WithMessage("New password must be different from current password.");
    }
}
