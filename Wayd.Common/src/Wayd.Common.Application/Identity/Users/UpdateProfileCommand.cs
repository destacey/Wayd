namespace Wayd.Common.Application.Identity.Users;

/// <summary>
/// The details a user may change on their own account. It carries no email: the email is also the username and
/// the key people sync links a user to an employee by, so only an identity provider, an administrator or the
/// sync sets it.
/// </summary>
public sealed record UpdateProfileCommand
{
    /// <summary>The user's first name.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>The user's last name.</summary>
    public string LastName { get; set; } = null!;

    /// <summary>The user's phone number, or <c>null</c> to clear it.</summary>
    public string? PhoneNumber { get; set; }
}
