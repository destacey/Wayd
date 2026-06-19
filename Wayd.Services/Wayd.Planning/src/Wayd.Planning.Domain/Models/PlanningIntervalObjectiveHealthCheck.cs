using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.HealthChecks;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalObjectiveHealthCheck : HealthCheckBase
{
    private PlanningIntervalObjectiveHealthCheck() { }

    internal PlanningIntervalObjectiveHealthCheck(
        Guid planningIntervalObjectiveId,
        HealthStatus status,
        Guid reportedById,
        Instant reportedOn,
        Instant expiration,
        string? note)
        : base(status, reportedById, reportedOn, expiration, note)
    {
        Guard.Against.Default(planningIntervalObjectiveId, nameof(planningIntervalObjectiveId));

        PlanningIntervalObjectiveId = planningIntervalObjectiveId;
    }

    public Guid PlanningIntervalObjectiveId { get; private init; }
}
