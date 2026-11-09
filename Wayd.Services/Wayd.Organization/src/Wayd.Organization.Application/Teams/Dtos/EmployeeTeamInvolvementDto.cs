namespace Wayd.Organization.Application.Teams.Dtos;

/// <summary>
/// The active teams and teams of teams an employee is a member of, counted once each however many roles they hold.
/// </summary>
public sealed record EmployeeTeamInvolvementDto
{
    /// <summary>How many active teams the employee is a member of.</summary>
    public int TeamCount { get; init; }

    /// <summary>How many active teams of teams the employee is a direct member of.</summary>
    public int TeamOfTeamsCount { get; init; }

    /// <summary>
    /// The ids of those teams and teams of teams, so another module can answer what the teams are linked to
    /// without reading memberships itself.
    /// </summary>
    public IReadOnlyList<Guid> TeamIds { get; init; } = [];
}
