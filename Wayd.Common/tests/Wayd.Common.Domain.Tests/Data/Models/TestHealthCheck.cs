using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.HealthChecks;

namespace Wayd.Common.Domain.Tests.Data.Models;

/// <summary>
/// Concrete implementation of <see cref="HealthCheckBase"/> for testing purposes.
/// </summary>
public sealed class TestHealthCheck : HealthCheckBase
{
    private TestHealthCheck() : base() { }

    private TestHealthCheck(HealthStatus status, Guid reportedById, Instant reportedOn, Instant expiration, string? note)
        : base(status, reportedById, reportedOn, expiration, note)
    {
    }

    public static TestHealthCheck Create(HealthStatus status, Guid reportedById, Instant reportedOn, Instant expiration, string? note)
        => new(status, reportedById, reportedOn, expiration, note);
}
