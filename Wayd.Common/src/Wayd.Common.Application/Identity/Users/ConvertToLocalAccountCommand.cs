namespace Wayd.Common.Application.Identity.Users;

public sealed record ConvertToLocalAccountCommand(string UserId, string NewPassword);

public sealed class ConvertToLocalAccountCommandValidator : CustomValidator<ConvertToLocalAccountCommand>
{
    public ConvertToLocalAccountCommandValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty();

        RuleFor(c => c.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}
