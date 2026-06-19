using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.HealthChecks;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

public sealed class ProjectHealthCheck : HealthCheckBase
{
    private ProjectHealthCheck() { }

    internal ProjectHealthCheck(
        Guid projectId,
        HealthStatus status,
        Guid reportedById,
        Instant reportedOn,
        Instant expiration,
        string? note)
        : base(status, reportedById, reportedOn, expiration, note)
    {
        Guard.Against.Default(projectId, nameof(projectId));

        ProjectId = projectId;
    }

    public Guid ProjectId { get; private init; }
}
