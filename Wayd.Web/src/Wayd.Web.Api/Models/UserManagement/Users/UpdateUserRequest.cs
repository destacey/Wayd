namespace Wayd.Web.Api.Models.UserManagement.Users;

public sealed record UpdateUserRequest
{
    public string Id { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? EmployeeId { get; set; }

    public UpdateUserCommand ToUpdateUserCommand()
        => new()
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            PhoneNumber = PhoneNumber,
            EmployeeId = EmployeeId,
            // The admin user-edit form owns the employee link, so it opts in to applying it —
            // including clearing it (EmployeeId = null unlinks). The self-service profile edit does not.
            ManageEmployeeLink = true
        };
}

/// <summary>
/// Validates a <see cref="UpdateUserRequest"/> by the rules of the <see cref="UpdateUserCommand"/> it maps to. The controller calls
/// <see cref="IUserService"/> directly rather than dispatching, so this is the only place those rules run.
/// </summary>
/// <remarks>The two share property names, so each failure is reported against the request's own property.</remarks>
public sealed class UpdateUserRequestValidator : CustomValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IValidator<UpdateUserCommand> commandValidator)
    {
        RuleFor(r => r).CustomAsync(async (request, context, cancellationToken) =>
        {
            var result = await commandValidator.ValidateAsync(request.ToUpdateUserCommand(), cancellationToken);
            foreach (var failure in result.Errors)
                context.AddFailure(failure);
        });
    }
}
