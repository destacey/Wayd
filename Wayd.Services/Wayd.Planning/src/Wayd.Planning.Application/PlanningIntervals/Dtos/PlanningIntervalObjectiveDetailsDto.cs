using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalObjectiveDetailsDto
{
    /// <summary>The objective's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The objective's numeric key, an alternate to its id.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the objective.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The description of the objective.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>The objective's status.</summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>The objective's current health check; null when it has none that has not expired.</summary>
    public PlanningHealthCheckDto? HealthCheck { get; set; }

    public double Progress { get; set; }

    public required NavigationDto PlanningInterval { get; set; }

    public required PlanningTeamNavigationDto Team { get; set; }

    /// <summary>The objective's type.</summary>
    public required SimpleNavigationDto Type { get; set; }

    /// <summary>The day work on the objective is planned to start; null when not set.</summary>
    public LocalDate? StartDate { get; set; }

    /// <summary>The day the objective is targeted to be done by; null when not set.</summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>When the objective moved to a closed status; null while it is open.</summary>
    public Instant? ClosedDate { get; set; }

    /// <summary>Whether the objective is a stretch objective rather than a committed one.</summary>
    public bool IsStretch { get; set; }

    public static PlanningIntervalObjectiveDetailsDto Create(PlanningIntervalObjective piObjective, NavigationDto piNavigationDto, Instant now)
    {
        return new PlanningIntervalObjectiveDetailsDto()
        {
            Id = piObjective.Id,
            Key = piObjective.Key,
            Name = piObjective.Name,
            Description = piObjective.Description,
            PlanningInterval = piNavigationDto,
            Status = SimpleNavigationDto.FromEnum(piObjective.Status),
            HealthCheck = PlanningHealthCheckDto.FromCurrent(piObjective.HealthChecks, now),
            Progress = piObjective.Progress,
            Team = PlanningTeamNavigationDto.FromPlanningTeam(piObjective.Team),
            Type = SimpleNavigationDto.FromEnum(piObjective.Type),
            StartDate = piObjective.StartDate,
            TargetDate = piObjective.TargetDate,
            ClosedDate = piObjective.ClosedDate,
            IsStretch = piObjective.IsStretch
        };
    }
}
