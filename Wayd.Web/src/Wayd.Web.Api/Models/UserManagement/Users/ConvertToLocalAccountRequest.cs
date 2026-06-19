namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record ConvertToLocalAccountRequest(string NewPassword);

public sealed class ConvertToLocalAccountRequestValidator : CustomValidator<ConvertToLocalAccountRequest>
{
    public ConvertToLocalAccountRequestValidator()
    {
        RuleFor(r => r.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}
