using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningIntervalObjectiveListDto
{
    /// <summary>The objective's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The objective's numeric key, an alternate to its id.</summary>
    public int Key { get; set; }

    /// <summary>
    /// The name of the objective.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>The objective's status.</summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>The objective's current health check; null when it has none that has not expired.</summary>
    public PlanningHealthCheckDto? HealthCheck { get; set; }

    public required NavigationDto PlanningInterval { get; set; }

    public required PlanningTeamNavigationDto Team { get; set; }

    public double Progress { get; set; }

    /// <summary>The objective's type.</summary>
    public required SimpleNavigationDto Type { get; set; }

    /// <summary>The day work on the objective is planned to start; null when not set.</summary>
    public LocalDate? StartDate { get; set; }

    /// <summary>The day the objective is targeted to be done by; null when not set.</summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>Whether the objective is a stretch objective rather than a committed one.</summary>
    public bool IsStretch { get; set; }

    /// <summary>
    /// The order of the Objective compared to other Objectives in the same context.
    /// </summary>
    public int? Order { get; set; }

    public static PlanningIntervalObjectiveListDto Create(PlanningIntervalObjective piObjective, NavigationDto piNavigationDto, Instant now)
    {
        return new PlanningIntervalObjectiveListDto()
        {
            Id = piObjective.Id,
            Key = piObjective.Key,
            Name = piObjective.Name,
            PlanningInterval = piNavigationDto,
            Status = SimpleNavigationDto.FromEnum(piObjective.Status),
            HealthCheck = PlanningHealthCheckDto.FromCurrent(piObjective.HealthChecks, now),
            Progress = piObjective.Progress,
            Team = PlanningTeamNavigationDto.FromPlanningTeam(piObjective.Team),
            Type = SimpleNavigationDto.FromEnum(piObjective.Type),
            StartDate = piObjective.StartDate,
            TargetDate = piObjective.TargetDate,
            IsStretch = piObjective.IsStretch,
            Order = piObjective.Order
        };
    }
}
