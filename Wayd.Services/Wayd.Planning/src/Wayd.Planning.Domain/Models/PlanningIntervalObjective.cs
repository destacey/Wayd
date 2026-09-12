using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Common.Extensions;
using Wayd.Planning.Domain.Enums;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalObjective : BaseSoftDeletableEntity, IHasIdAndKey
{
    private readonly List<PlanningIntervalObjectiveHealthCheck> _healthChecks = [];

    private PlanningIntervalObjective() { }

    internal PlanningIntervalObjective(Guid planningIntervalId, Guid teamId, string name, string? description, PlanningIntervalObjectiveType type, bool isStretch, LocalDate? startDate, LocalDate? targetDate, int? order)
    {
        Status = ObjectiveStatus.NotStarted;
        Progress = 0.0d;

        PlanningIntervalId = planningIntervalId;
        TeamId = teamId;
        Name = name;
        Description = description;
        Type = type;
        IsStretch = isStretch;
        StartDate = startDate;
        TargetDate = targetDate;
        Order = order;
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

    /// <summary>
    /// The name of the objective.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The description of the objective.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>Gets or sets the type.</summary>
    /// <value>The PI objective type.</value>
    public PlanningIntervalObjectiveType Type { get; private init; }

    /// <summary>Gets or sets the status.</summary>
    /// <value>The status.</value>
    public ObjectiveStatus Status { get; private set; }

    /// <summary>Gets the progress percentage.</summary>
    /// <value>The progress percentage.</value>
    public double Progress
    {
        get;
        private set => field = value < 0
            ? 0.0d
            : value > 100
                ? 100.0d
                : value;
    }

    /// <summary>Gets or sets the start date.</summary>
    /// <value>The start date.</value>
    public LocalDate? StartDate { get; private set; }

    /// <summary>Gets or sets the target date.</summary>
    /// <value>The target date.</value>
    public LocalDate? TargetDate { get; private set; }

    /// <summary>Gets the closed date.</summary>
    /// <value>The closed date.</value>
    public Instant? ClosedDate { get; private set; }

    /// <summary>
    /// The order of the objective compared to other objectives in the same planning interval.
    /// </summary>
    public int? Order { get; private set; }

    /// <summary>Gets a value indicating whether this instance is stretch.</summary>
    /// <value><c>true</c> if this instance is stretch; otherwise, <c>false</c>.</value>
    public bool IsStretch { get; private set; } = false;

    /// <summary>
    /// Full history of health checks for this objective, ordered by EF as loaded.
    /// Domain invariant: at most one check is non-expired at any instant.
    /// </summary>
    public IReadOnlyCollection<PlanningIntervalObjectiveHealthCheck> HealthChecks => _healthChecks.AsReadOnly();

    internal Result Update(string name, string? description, ObjectiveStatus status, double progress, LocalDate? startDate, LocalDate? targetDate, bool isStretch, Instant timestamp)
    {
        try
        {
            ChangeStatus(status, timestamp);

            Name = name;
            Description = description;
            Progress = progress;
            StartDate = startDate;
            TargetDate = targetDate;
            IsStretch = isStretch;

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    internal void UpdateOrder(int? order)
    {
        Order = order;
    }

    private void ChangeStatus(ObjectiveStatus status, Instant timestamp)
    {
        if (Status == status) return;

        if (IsClosed(Status) && !IsClosed(status))
        {
            ClosedDate = null;
        }
        else if (IsClosed(status))
        {
            ClosedDate = timestamp;
        }

        Status = status;
    }

    private static bool IsClosed(ObjectiveStatus status)
        => status is ObjectiveStatus.Completed or ObjectiveStatus.Canceled or ObjectiveStatus.Missed;

    /// <summary>
    /// Creates an objective from an external source, with its status, progress and closed date already
    /// known. The caller is responsible for the closed date agreeing with the status.
    /// </summary>
    internal static PlanningIntervalObjective Import(Guid planningIntervalId, Guid teamId, string name, string? description, PlanningIntervalObjectiveType type, ObjectiveStatus status, double progress, bool isStretch, LocalDate? startDate, LocalDate? targetDate, Instant? closedDate, int? order)
    {
        return new PlanningIntervalObjective(planningIntervalId, teamId, name, description, type, isStretch, startDate, targetDate, order)
        {
            Status = status,
            Progress = progress,
            ClosedDate = closedDate
        };
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
