using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalObjective : BaseSoftDeletableEntity, IHasIdAndKey
{
    private readonly List<PlanningIntervalObjectiveHealthCheck> _healthChecks = [];

    private PlanningIntervalObjective() { }

    internal PlanningIntervalObjective(Guid planningIntervalId, Guid teamId, Guid objectiveId, PlanningIntervalObjectiveType type, bool isStretch)
    {
        Status = ObjectiveStatus.NotStarted;

        PlanningIntervalId = planningIntervalId;
        TeamId = teamId;
        ObjectiveId = objectiveId;
        Type = type;
        IsStretch = isStretch;
    }

    /// <summary>Gets the key.</summary>
    /// <value>The key.</value>
    public int Key { get; private init; }

    /// <summary>Gets the planning interval identifier.</summary>
    /// <value>The planning interval identifier.</value>
    public Guid PlanningIntervalId { get; private init; }

    /// <summary>Gets the team identifier.</summary>
    /// <value>The team identifier.</value>
    public Guid TeamId { get; private init; }

    /// <summary>Gets the team.</summary>
    /// <value>The team.</value>
    public PlanningTeam Team { get; private set; } = default!;

    /// <summary>Gets the objective identifier.</summary>
    /// <value>The objective identifier.</value>
    public Guid ObjectiveId { get; init; }

    /// <summary>Gets or sets the type.</summary>
    /// <value>The PI objective type.</value>
    public PlanningIntervalObjectiveType Type { get; private init; }

    /// <summary>Gets or sets the status.</summary>
    /// <value>The status.</value>
    public ObjectiveStatus Status { get; private set; }

    /// <summary>Gets a value indicating whether this instance is stretch.</summary>
    /// <value><c>true</c> if this instance is stretch; otherwise, <c>false</c>.</value>
    public bool IsStretch { get; private set; } = false;

    /// <summary>
    /// Full history of health checks for this objective, ordered by EF as loaded.
    /// Domain invariant: at most one check is non-expired at any instant.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalObjectiveHealthCheck> HealthChecks => _healthChecks.AsReadOnly();

    /// <summary>Updates the specified PI objective.</summary>
    /// <param name="status">The status.</param>
    /// <param name="isStretch">if set to <c>true</c> [is stretch].</param>
    /// <returns></returns>
    internal Result Update(ObjectiveStatus status, bool isStretch)
    {
        Status = status;
        IsStretch = isStretch;

        return Result.Success();
    }

    public Result<PlanningIntervalObjectiveHealthCheck> AddHealthCheck(HealthStatus status, Guid reportedById, Instant expiration, string? note, Instant now)
    {
        if (expiration <= now)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>("Expiration must be in the future.");

        var newCheck = new PlanningIntervalObjectiveHealthCheck(Id, status, reportedById, now, expiration, note);

        var report = new HealthReport<PlanningIntervalObjectiveHealthCheck>(_healthChecks);
        report.Add(newCheck, now);

        _healthChecks.Add(newCheck);

        return Result.Success(newCheck);
    }

    public Result<PlanningIntervalObjectiveHealthCheck> UpdateHealthCheck(Guid healthCheckId, HealthStatus status, Instant expiration, string? note, Instant now)
    {
        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>($"Health check {healthCheckId} not found on objective {Id}.");

        var updateResult = healthCheck.Update(status, expiration, note, now);
        if (updateResult.IsFailure)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>(updateResult.Error);

        return Result.Success(healthCheck);
    }

    public Result<PlanningIntervalObjectiveHealthCheck> RemoveHealthCheck(Guid healthCheckId)
    {
        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>($"Health check {healthCheckId} not found on objective {Id}.");

        _healthChecks.Remove(healthCheck);
        return Result.Success(healthCheck);
    }
}
