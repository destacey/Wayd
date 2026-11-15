namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record CreateUserRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }
    public required string LoginProvider { get; set; }

    /// <summary>
    /// Microsoft Entra ID users only: the tenant their first sign-in is linked from.
    /// Optional when the Entra provider allows a single tenant.
    /// </summary>
    public string? TenantId { get; set; }

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
            TenantId = TenantId,
            Password = Password,
            RoleNames = RoleNames
        };
}

/// <summary>
/// Validates a <see cref="CreateUserRequest"/> by the rules of the <see cref="CreateUserCommand"/> it maps to. The controller calls
/// <see cref="IUserService"/> directly rather than dispatching, so this is the only place those rules run.
/// </summary>
/// <remarks>The two share property names, so each failure is reported against the request's own property.</remarks>
public sealed class CreateUserRequestValidator : CustomValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IValidator<CreateUserCommand> commandValidator)
    {
        RuleFor(r => r).CustomAsync(async (request, context, cancellationToken) =>
        {
            var result = await commandValidator.ValidateAsync(request.ToCreateUserCommand(), cancellationToken);
            foreach (var failure in result.Errors)
                context.AddFailure(failure);
        });
    }
}
