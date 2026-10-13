using NodaTime;
using Wayd.Common.Application.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// A team's capacity over a date range, from the time people are allocated to it and the days it works.
/// Capacity is in person-days: one person allocated in full for one working day.
/// </summary>
public sealed record TeamCapacityDto
{
    /// <summary>The first day of the range.</summary>
    public required LocalDate From { get; set; }

    /// <summary>The last day of the range.</summary>
    public required LocalDate To { get; set; }

    /// <summary>The team's working days in the range: days in its working week that are not holidays.</summary>
    public required int WorkingDays { get; set; }

    /// <summary>The time allocated to the team in every role, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of the allocated time in roles that count toward delivery capacity, in person-days.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>Whether the caller may change this team's allocations.</summary>
    public bool CanManage { get; set; }

    /// <summary>Everyone with time allocated to the team in the range, by name.</summary>
    public required IReadOnlyList<TeamCapacityPersonDto> People { get; set; }
}

/// <summary>One person's time on a team over a date range.</summary>
public sealed record TeamCapacityPersonDto
{
    public required NavigationDto Employee { get; set; }

    /// <summary>Their time allocated to the team in every role, in person-days.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of it in roles that count toward delivery capacity, in person-days.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>Their time allocated to other teams on this team's working days, in person-days.</summary>
    public required decimal ElsewherePersonDays { get; set; }

    /// <summary>Their allocations to the team that fall in the range, in date order.</summary>
    public required IReadOnlyList<TeamCapacityAllocationDto> Allocations { get; set; }
}

/// <summary>An allocation of a person's time to the team.</summary>
public sealed record TeamCapacityAllocationDto
{
    public required Guid Id { get; set; }

    /// <summary>The team member role the time is spent in, or null when none was chosen.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether the allocation adds to the team's delivery capacity. True when it has no role.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The first day of the allocation, which may be before the range.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day of the allocation, or null when it has no end. It may be after the range.</summary>
    public LocalDate? End { get; set; }

    /// <summary>The share of the person's time, from 1 to 100.</summary>
    public required int Percent { get; set; }
}
