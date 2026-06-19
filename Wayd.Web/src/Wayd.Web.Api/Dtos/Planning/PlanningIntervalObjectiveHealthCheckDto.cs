using Wayd.Common.Application.Dtos;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Web.Api.Dtos.Planning;

public sealed record PlanningIntervalObjectiveHealthCheckDto
{
    /// <summary>Gets or sets the identifier.</summary>
    /// <value>The identifier.</value>
    public Guid Id { get; set; }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; set; }

    /// <summary>
    /// The name of the objective.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the status.</summary>
    /// <value>The status.</value>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>Gets or sets the type.</summary>
    /// <value>The PI objective type.</value>
    public required SimpleNavigationDto Type { get; set; }

    public required NavigationDto PlanningInterval { get; set; }

    public required PlanningTeamNavigationDto Team { get; set; }

    public double Progress { get; set; }

    /// <summary>Gets a value indicating whether this instance is stretch.</summary>
    /// <value><c>true</c> if this instance is stretch; otherwise, <c>false</c>.</value>
    public bool IsStretch { get; set; }

    /// <summary>
    /// The id of the health check.
    /// </summary>
    public Guid? HealthCheckId { get; set; }

    /// <summary>
    /// The status of the health check.
    /// </summary>
    public SimpleNavigationDto? HealthStatus { get; set; }

    /// <summary>
    /// The timestamp of when the health check was reported.
    /// </summary>
    public Instant? ReportedOn { get; set; }

    /// <summary>
    /// The expiration of the health check.
    /// </summary>
    public Instant? Expiration { get; set; }

    /// <summary>
    /// The note for the health check.
    /// </summary>
    public string? Note { get; set; }

    public static PlanningIntervalObjectiveHealthCheckDto Create(PlanningIntervalObjectiveListDto objective)
    {
        return new PlanningIntervalObjectiveHealthCheckDto()
        {
            Id = objective.Id,
            Key = objective.Key,
            Name = objective.Name,
            Status = objective.Status,
            Type = objective.Type,
            PlanningInterval = objective.PlanningInterval,
            Team = objective.Team,
            Progress = objective.Progress,
            IsStretch = objective.IsStretch,
            HealthCheckId = objective.HealthCheck?.Id,
            HealthStatus = objective.HealthCheck?.Status,
            ReportedOn = objective.HealthCheck?.ReportedOn,
            Expiration = objective.HealthCheck?.Expiration,
            Note = objective.HealthCheck?.Note
        };
    }
}
