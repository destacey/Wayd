using Wayd.Common.Application.Dtos;

namespace Wayd.Web.Api.Models.Me;

/// <summary>
/// Who the caller is: their sign-in account and, more often what matters, the employee that account is
/// linked to. Wayd records people as employees, so roles, assignees, team memberships and allocations
/// all name the employee, not the account.
/// </summary>
public sealed record MeResponse
{
    /// <summary>
    /// The caller's user account id. Pass it only where a parameter's description says it takes a user
    /// id; role, assignee and employeeId parameters take <see cref="Employee"/>'s id instead.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>The account's user name, usually the sign-in email.</summary>
    public string? UserName { get; init; }

    /// <summary>The account's first name.</summary>
    public string? FirstName { get; init; }

    /// <summary>The account's last name.</summary>
    public string? LastName { get; init; }

    /// <summary>The account's email address.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// The employee the account is linked to: its UUID `id` is what employeeId, role and assignee
    /// parameters take for the caller. Null when the account has no linked employee, in which case
    /// nothing in Wayd is recorded against the caller as a person.
    /// </summary>
    public NavigationDto? Employee { get; init; }

    /// <summary>The names of the roles that grant the caller's permissions.</summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
