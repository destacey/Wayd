using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Replication;
using Wayd.Work.Domain.Interfaces;
using NodaTime;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The Work module's copy of a Planning iteration, kept by the <c>Iteration*</c> events (see
/// <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
public sealed class WorkIteration : BaseEntity<Guid>, ISimpleIteration, IHasIdAndKey, IHasOptionalWorkTeam
{
    private WorkIteration() { }

    /// <param name="iteration">The iteration's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public WorkIteration(ISimpleIteration iteration, Instant asOf)
    {
        Id = iteration.Id;
        Key = iteration.Key;
        Name = iteration.Name;
        Type = iteration.Type;
        State = iteration.State;
        DateRange = iteration.DateRange;
        TeamId = iteration.TeamId;
        Watermarks = WorkIterationWatermarks.At(asOf);
    }

    public int Key { get; private init; }
    public string Name { get; private set; } = default!;
    public IterationType Type { get; private set; }
    public IterationState State { get; private set; }
    public IterationDateRange DateRange { get; private set; } = default!;
    public Guid? TeamId { get; private set; }
    public WorkTeam? Team { get; private set; }
    public WorkIterationWatermarks Watermarks { get; private set; } = WorkIterationWatermarks.None;

    /// <summary>Applies a change to the name and type made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDetails(string name, IterationType type, EventActor actor, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Details, timestamp))
            return false;

        var changed = AssignDetails(name, type, actor, timestamp);
        if (!changed && Watermarks.Details == timestamp)
            return false;

        Watermarks = Watermarks with { Details = timestamp };
        return true;
    }

    /// <summary>Applies a change to the date range made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDateRange(IterationDateRange dateRange, EventActor actor, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.DateRange, timestamp))
            return false;

        var changed = AssignDateRange(dateRange, actor, timestamp);
        if (!changed && Watermarks.DateRange == timestamp)
            return false;

        Watermarks = Watermarks with { DateRange = timestamp };
        return true;
    }

    /// <summary>Applies a change of state made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyState(IterationState state, EventActor actor, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.State, timestamp))
            return false;

        var changed = AssignState(state, actor, timestamp);
        if (!changed && Watermarks.State == timestamp)
            return false;

        Watermarks = Watermarks with { State = timestamp };
        return true;
    }

    /// <summary>Applies a change of team made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyTeam(Guid? teamId, EventActor actor, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Team, timestamp))
            return false;

        var changed = AssignTeam(teamId, actor, timestamp);
        if (!changed && Watermarks.Team == timestamp)
            return false;

        Watermarks = Watermarks with { Team = timestamp };
        return true;
    }

    /// <summary>
    /// Applies the whole iteration as a change made at <paramref name="timestamp"/> left it, one group at a time.
    /// Only the superseded whole-record event needs this.
    /// </summary>
    /// <returns>Whether any group applied.</returns>
    public bool ApplyRecord(ISimpleIteration iteration, EventActor actor, Instant timestamp)
    {
        EnsureSameIteration(iteration);

        var details = ApplyDetails(iteration.Name, iteration.Type, actor, timestamp);
        var dateRange = ApplyDateRange(iteration.DateRange, actor, timestamp);
        var state = ApplyState(iteration.State, actor, timestamp);
        var team = ApplyTeam(iteration.TeamId, actor, timestamp);

        return details || dateRange || state || team;
    }

    /// <summary>
    /// Brings the copy in line with the source as read at <paramref name="asOf"/>, one group at a time.
    /// </summary>
    /// <remarks>
    /// A group that already matches keeps its watermark: stamping it would skip a change the read could not
    /// see, one timestamped before <paramref name="asOf"/> but committed after it. A group holding a change
    /// newer than the read is left alone too, because the read is the older of the two.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Resync(ISimpleIteration iteration, EventActor actor, Instant asOf)
    {
        EnsureSameIteration(iteration);

        var changed = false;

        if (!ReplicaWatermark.IsStale(Watermarks.Details, asOf) && AssignDetails(iteration.Name, iteration.Type, actor, asOf))
        {
            Watermarks = Watermarks with { Details = asOf };
            changed = true;
        }

        if (!ReplicaWatermark.IsStale(Watermarks.DateRange, asOf) && AssignDateRange(iteration.DateRange, actor, asOf))
        {
            Watermarks = Watermarks with { DateRange = asOf };
            changed = true;
        }

        if (!ReplicaWatermark.IsStale(Watermarks.State, asOf) && AssignState(iteration.State, actor, asOf))
        {
            Watermarks = Watermarks with { State = asOf };
            changed = true;
        }

        if (!ReplicaWatermark.IsStale(Watermarks.Team, asOf) && AssignTeam(iteration.TeamId, actor, asOf))
        {
            Watermarks = Watermarks with { Team = asOf };
            changed = true;
        }

        return changed;
    }

    private bool AssignDetails(string name, IterationType type, EventActor actor, Instant timestamp)
    {
        var previous = new IterationDetails(Name, Type);
        var details = new IterationDetails(name, type);
        if (details == previous)
            return false;

        Name = name;
        Type = type;
        AddDomainEvent(new WorkIterationDetailsUpdatedEvent(Id, Key, Name, Type, previous, actor, timestamp));
        return true;
    }

    private bool AssignDateRange(IterationDateRange dateRange, EventActor actor, Instant timestamp)
    {
        var previous = DateRange;
        if (dateRange == previous)
            return false;

        DateRange = dateRange;
        AddDomainEvent(new WorkIterationDateRangeChangedEvent(Id, Key, previous, DateRange, actor, timestamp));
        return true;
    }

    private bool AssignState(IterationState state, EventActor actor, Instant timestamp)
    {
        var previous = State;
        if (state == previous)
            return false;

        State = state;
        AddDomainEvent(new WorkIterationStateChangedEvent(Id, Key, previous, State, actor, timestamp));
        return true;
    }

    private bool AssignTeam(Guid? teamId, EventActor actor, Instant timestamp)
    {
        var previous = TeamId;
        if (teamId == previous)
            return false;

        TeamId = teamId;
        AddDomainEvent(new WorkIterationTeamChangedEvent(Id, Key, previous, TeamId, actor, timestamp));
        return true;
    }

    private void EnsureSameIteration(ISimpleIteration iteration)
    {
        if (iteration.Id != Id)
        {
            throw new InvalidOperationException("Cannot apply a different iteration to this WorkIteration.");
        }
    }
}
