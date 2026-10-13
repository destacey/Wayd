using NodaTime;
using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// Allocations a team could start from: its members, in the roles they hold, at what is left of their time.
/// Nothing in it is saved.
/// </summary>
public sealed record TeamAllocationProposalDto
{
    /// <summary>The first day the proposed allocations would cover.</summary>
    public required LocalDate From { get; set; }

    /// <summary>
    /// One row per member and role, by member name and then role name. A member who already has time on the
    /// team on <see cref="From"/> has no rows.
    /// </summary>
    public required IReadOnlyList<TeamAllocationProposalRowDto> Rows { get; set; }
}

/// <summary>A proposed allocation of a member's time to the team in one role.</summary>
public sealed record TeamAllocationProposalRowDto
{
    /// <summary>The member whose time is proposed.</summary>
    public required NavigationDto Employee { get; set; }

    /// <summary>The team member role the member holds.</summary>
    public required NavigationDto Role { get; set; }

    /// <summary>Whether the role's time adds to the team's delivery capacity.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>
    /// The last day the proposed allocation would cover: the day before the member's next allocation to the
    /// team, so that one is left as it is. Null, for no end, when they have none.
    /// </summary>
    public LocalDate? End { get; set; }

    /// <summary>
    /// What is left of the member's time across all teams, from 0 to 100: 100 less their most allocated day
    /// from <see cref="TeamAllocationProposalDto.From"/> to <see cref="End"/>. Zero means they have none to give.
    /// </summary>
    public required int RemainingPercent { get; set; }

    /// <summary>
    /// The share proposed for this role, from 0 to 100: the member's remaining time split evenly between the
    /// roles they hold on the team.
    /// </summary>
    public required int Percent { get; set; }
}
