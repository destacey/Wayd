namespace Wayd.Web.Api.Models.UserManagement.Profiles;

/// <summary>
/// The details a user may change on their own account. The email is not among them: it is also the username and
/// the address people sync links a user to an employee by, so an administrator changes it from Settings → Access → Users.
/// </summary>
public sealed record UpdateProfileRequest
{
    /// <summary>The id of the user making the request.</summary>
    public string Id { get; set; } = default!;

    /// <summary>The user's first name.</summary>
    public string FirstName { get; set; } = default!;

    /// <summary>The user's last name.</summary>
    public string LastName { get; set; } = default!;

    /// <summary>The user's phone number; empty clears it.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Maps the request to the command the user service applies.</summary>
    public UpdateProfileCommand ToUpdateProfileCommand()
        => new()
        {
            FirstName = FirstName,
            LastName = LastName,
            PhoneNumber = PhoneNumber
        };
}

/// <summary>Validates <see cref="UpdateProfileRequest"/>.</summary>
public sealed class UpdateProfileRequestValidator : CustomValidator<UpdateProfileRequest>
{
    /// <summary>Requires the id and both names, each at most 100 characters.</summary>
    public UpdateProfileRequestValidator()
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
    }
}
