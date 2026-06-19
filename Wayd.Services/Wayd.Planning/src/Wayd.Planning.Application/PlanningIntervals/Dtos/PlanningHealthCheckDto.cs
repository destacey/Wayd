using Wayd.Common.Application.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Dtos;

public sealed record PlanningHealthCheckDto
{
    /// <summary>
    /// The id of the health check.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The status of the health check.
    /// </summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>
    /// The employee who reported the health check.
    /// </summary>
    public required NavigationDto ReportedBy { get; set; }

    /// <summary>
    /// The timestamp of when the health check was reported.
    /// </summary>
    public Instant ReportedOn { get; set; }

    /// <summary>
    /// The expiration of the health check.
    /// </summary>
    public Instant Expiration { get; set; }

    /// <summary>
    /// The note for the health check.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Returns the active (non-expired) health check from the supplied collection,
    /// or <c>null</c> if none are active. The domain invariant guarantees at most one active check at a time.
    /// </summary>
    public static PlanningHealthCheckDto? FromCurrent(IEnumerable<PlanningIntervalObjectiveHealthCheck> healthChecks, Instant now)
    {
        var current = healthChecks.FirstOrDefault(h => !h.IsExpired(now));
        if (current is null)
            return null;

        return new PlanningHealthCheckDto
        {
            Id = current.Id,
            Status = SimpleNavigationDto.FromEnum(current.Status),
            ReportedBy = NavigationDto.Create(current.ReportedBy.Id, current.ReportedBy.Key, current.ReportedBy.Name.DisplayName),
            ReportedOn = current.ReportedOn,
            Expiration = current.Expiration,
            Note = current.Note
        };
    }
}
