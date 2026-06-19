using NodaTime;

namespace Wayd.Common.Domain.Models.HealthChecks;

public sealed class HealthReport<THealthCheck> where THealthCheck : HealthCheckBase
{
    private readonly List<THealthCheck> _healthChecks;

    public HealthReport(IEnumerable<THealthCheck> existing)
    {
        _healthChecks = [.. existing.OrderByDescending(h => h.ReportedOn)];
    }

    public IReadOnlyList<THealthCheck> HealthChecks => _healthChecks.AsReadOnly();

    /// <summary>
    /// Add a new health check to the report. If the latest existing check has not yet expired,
    /// its expiration is truncated to <paramref name="now"/> so that no two checks are active simultaneously.
    /// </summary>
    public void Add(THealthCheck newCheck, Instant now)
    {
        var latest = _healthChecks.FirstOrDefault();
        if (latest is not null && now < latest.Expiration)
        {
            latest.ChangeExpiration(now);
        }

        _healthChecks.Insert(0, newCheck);
    }
}
