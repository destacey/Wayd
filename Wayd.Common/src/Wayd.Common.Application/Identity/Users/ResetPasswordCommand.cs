namespace Wayd.Common.Application.Identity.Users;

public sealed record ResetPasswordCommand(string UserId, string NewPassword);

public sealed class ResetPasswordCommandValidator : CustomValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(p => p.UserId)
            .NotEmpty();

        RuleFor(p => p.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters.");
    }
}
