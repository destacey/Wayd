using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Models.HealthChecks;
using Wayd.Common.Extensions;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

public sealed class PlanningIntervalObjective : BaseSoftDeletableEntity, IHasIdAndKey
{
    private readonly List<PlanningIntervalObjectiveHealthCheck> _healthChecks = [];

    private PlanningIntervalObjective() { }

    private PlanningIntervalObjective(Guid planningIntervalId, Guid teamId, string name, string? description, PlanningIntervalObjectiveType type, bool isStretch, LocalDate? startDate, LocalDate? targetDate, int? order)
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

    /// <summary>
    /// Applies an edit. Each part that changed raises its own event: the details, the status, the progress, the
    /// stretch flag and the timeline change for different reasons.
    /// </summary>
    internal Result Update(string name, string? description, ObjectiveStatus status, double progress, LocalDate? startDate, LocalDate? targetDate, bool isStretch, EventActor actor, Instant timestamp)
    {
        try
        {
            var previousDetails = new PlanningIntervalObjectiveDetails(Name, Description);
            var previousProgress = Progress;
            var previousIsStretch = IsStretch;
            var previousStartDate = StartDate;
            var previousTargetDate = TargetDate;

            ChangeStatus(status, actor, timestamp);

            Name = name;
            Description = description;
            Progress = progress;
            StartDate = startDate;
            TargetDate = targetDate;
            IsStretch = isStretch;

            // Compared after assignment: the text setters trim and the progress setter clamps.
            var details = new PlanningIntervalObjectiveDetails(Name, Description);
            if (details != previousDetails)
                AddKeyedDomainEvent(() => new PlanningIntervalObjectiveDetailsUpdatedEvent(Id, Key, details.Name, details.Description, previousDetails, actor, timestamp));

            var newProgress = Progress;
            if (newProgress != previousProgress)
                AddKeyedDomainEvent(() => new PlanningIntervalObjectiveProgressChangedEvent(Id, Key, previousProgress, newProgress, actor, timestamp));

            var newIsStretch = IsStretch;
            if (newIsStretch != previousIsStretch)
                AddKeyedDomainEvent(() => new PlanningIntervalObjectiveStretchChangedEvent(Id, Key, newIsStretch, actor, timestamp));

            var newStartDate = StartDate;
            var newTargetDate = TargetDate;
            if (newStartDate != previousStartDate || newTargetDate != previousTargetDate)
                AddKeyedDomainEvent(() => new PlanningIntervalObjectiveTimelineChangedEvent(Id, Key,
                    previousStartDate, previousTargetDate, newStartDate, newTargetDate, actor, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    internal void UpdateOrder(int? order, EventActor actor, Instant timestamp)
    {
        var previousOrder = Order;
        if (previousOrder == order) return;

        Order = order;

        AddKeyedDomainEvent(() => new PlanningIntervalObjectiveOrderChangedEvent(Id, Key, previousOrder, order, actor, timestamp));
    }

    /// <summary>
    /// Raises the deletion event. The caller removes the objective in the same save, which is what drains it.
    /// </summary>
    internal void Delete(EventActor actor, Instant timestamp)
    {
        AddDomainEvent(new PlanningIntervalObjectiveDeletedEvent(Id, Key, PlanningIntervalId, Name, actor, timestamp));
    }

    private void ChangeStatus(ObjectiveStatus status, EventActor actor, Instant timestamp)
    {
        if (Status == status) return;

        var previousStatus = Status;
        var previousClosedDate = ClosedDate;

        if (IsClosed(Status) && !IsClosed(status))
        {
            ClosedDate = null;
        }
        else if (IsClosed(status))
        {
            ClosedDate = timestamp;
        }

        Status = status;

        var closedDate = ClosedDate;
        AddKeyedDomainEvent(() => new PlanningIntervalObjectiveStatusChangedEvent(Id, Key, previousStatus, status, previousClosedDate, closedDate, actor, timestamp));
    }

    private static bool IsClosed(ObjectiveStatus status)
        => status is ObjectiveStatus.Completed or ObjectiveStatus.Canceled or ObjectiveStatus.Missed;

    /// <summary>
    /// Raises an event whose payload carries <see cref="Key"/>, which the first save assigns; a change made
    /// before it waits for the key. <paramref name="build"/> runs at that point, so capture what it reads.
    /// </summary>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }

    /// <summary>
    /// Raises the creation event once the first save assigns <see cref="Key"/>. Every other value is captured
    /// now, so a caller that changes the objective before that save cannot rewrite the creation.
    /// </summary>
    private void RaiseCreated(EventActor actor, Instant timestamp)
    {
        var planningIntervalId = PlanningIntervalId;
        var teamId = TeamId;
        var name = Name;
        var description = Description;
        var type = Type;
        var status = Status;
        var progress = Progress;
        var isStretch = IsStretch;
        var startDate = StartDate;
        var targetDate = TargetDate;
        var closedDate = ClosedDate;
        var order = Order;

        AddPostPersistenceAction(() => AddDomainEvent(new PlanningIntervalObjectiveCreatedEvent(
            Id, Key, planningIntervalId, teamId, name, description, type, status, progress, isStretch, startDate,
            targetDate, closedDate, order, actor, timestamp)));
    }

    internal static PlanningIntervalObjective Create(Guid planningIntervalId, Guid teamId, string name, string? description, PlanningIntervalObjectiveType type, bool isStretch, LocalDate? startDate, LocalDate? targetDate, int? order, EventActor actor, Instant timestamp)
    {
        var objective = new PlanningIntervalObjective(planningIntervalId, teamId, name, description, type, isStretch, startDate, targetDate, order);
        objective.RaiseCreated(actor, timestamp);

        return objective;
    }

    /// <summary>
    /// Creates an objective from an external source, with its status, progress and closed date already
    /// known. The caller is responsible for the closed date agreeing with the status.
    /// </summary>
    internal static PlanningIntervalObjective Import(Guid planningIntervalId, Guid teamId, string name, string? description, PlanningIntervalObjectiveType type, ObjectiveStatus status, double progress, bool isStretch, LocalDate? startDate, LocalDate? targetDate, Instant? closedDate, int? order, EventActor actor, Instant timestamp)
    {
        var objective = new PlanningIntervalObjective(planningIntervalId, teamId, name, description, type, isStretch, startDate, targetDate, order)
        {
            Status = status,
            Progress = progress,
            ClosedDate = closedDate
        };
        objective.RaiseCreated(actor, timestamp);

        return objective;
    }

    public Result<PlanningIntervalObjectiveHealthCheck> AddHealthCheck(HealthStatus status, Guid reportedById, Instant expiration, string? note, EventActor actor, Instant now)
    {
        if (expiration <= now)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>("Expiration must be in the future.");

        var newCheck = new PlanningIntervalObjectiveHealthCheck(Id, status, reportedById, now, expiration, note);

        var report = new HealthReport<PlanningIntervalObjectiveHealthCheck>(_healthChecks);
        report.Add(newCheck, now);

        _healthChecks.Add(newCheck);

        // From the stored check, not the arguments: the note is trimmed on the way in.
        AddKeyedDomainEvent(() => new PlanningIntervalObjectiveHealthCheckAddedEvent(
            Id, Key, newCheck.Id, newCheck.Status, newCheck.Note, newCheck.Expiration, reportedById, actor, now));

        return Result.Success(newCheck);
    }

    public Result<PlanningIntervalObjectiveHealthCheck> UpdateHealthCheck(Guid healthCheckId, HealthStatus status, Instant expiration, string? note, EventActor actor, Instant now)
    {
        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>($"Health check {healthCheckId} not found on objective {Id}.");

        // Compared after the update, never against the arguments: the note is trimmed on the way in.
        var before = (healthCheck.Status, healthCheck.Note, healthCheck.Expiration);

        var updateResult = healthCheck.Update(status, expiration, note, now);
        if (updateResult.IsFailure)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>(updateResult.Error);

        var after = (healthCheck.Status, healthCheck.Note, healthCheck.Expiration);
        if (before != after)
        {
            AddKeyedDomainEvent(() => new PlanningIntervalObjectiveHealthCheckUpdatedEvent(
                Id, Key, healthCheckId,
                before.Status, before.Note, before.Expiration,
                after.Status, after.Note, after.Expiration,
                actor, now));
        }

        return Result.Success(healthCheck);
    }

    public Result<PlanningIntervalObjectiveHealthCheck> RemoveHealthCheck(Guid healthCheckId, EventActor actor, Instant now)
    {
        var healthCheck = _healthChecks.FirstOrDefault(h => h.Id == healthCheckId);
        if (healthCheck is null)
            return Result.Failure<PlanningIntervalObjectiveHealthCheck>($"Health check {healthCheckId} not found on objective {Id}.");

        _healthChecks.Remove(healthCheck);

        var removedStatus = healthCheck.Status;
        AddKeyedDomainEvent(() => new PlanningIntervalObjectiveHealthCheckRemovedEvent(Id, Key, healthCheckId, removedStatus, actor, now));

        return Result.Success(healthCheck);
    }
}
