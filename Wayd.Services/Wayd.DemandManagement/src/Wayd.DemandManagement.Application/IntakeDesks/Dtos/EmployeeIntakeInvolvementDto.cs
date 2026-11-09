namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// What an employee is part of in intake: the desks they are named on, by role, and the open requests they raised.
/// </summary>
public sealed record EmployeeIntakeInvolvementDto
{
    /// <summary>How many desks the employee is a member of, in any role.</summary>
    public int DeskCount { get; init; }

    /// <summary>How many of those desks the employee is a Desk Admin on.</summary>
    public int AdminDeskCount { get; init; }

    /// <summary>How many of those desks the employee is an Agent on.</summary>
    public int AgentDeskCount { get; init; }

    /// <summary>
    /// How many requests in a Proposed or Active status the employee is the requester of or submitted: the
    /// requests the list shows under Mine with Open only.
    /// </summary>
    public int OpenRequestsRaisedCount { get; init; }
}
