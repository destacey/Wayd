using Wayd.Common.Domain.Enums.Planning;
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

    /// <summary>
    /// Applies the iteration as a change made at <paramref name="timestamp"/> left it.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyRecord(ISimpleIteration iteration, EventActor actor, Instant timestamp)
    {
        EnsureSameIteration(iteration);

        if (ReplicaWatermark.IsStale(Watermarks.Record, timestamp))
        {
            return false;
        }

        var valuesChanged = Assign(iteration, actor, timestamp);
        if (!valuesChanged && Watermarks.Record == timestamp)
        {
            return false;
        }

        Watermarks = Watermarks with { Record = timestamp };
        return true;
    }

    /// <summary>
    /// Brings the copy in line with the source as read at <paramref name="asOf"/>.
    /// </summary>
    /// <remarks>
    /// A copy that already matches keeps its watermark: stamping it would skip a change the read could not
    /// see, one timestamped before <paramref name="asOf"/> but committed after it. A copy holding a change
    /// newer than the read is left alone too, because the read is the older of the two.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Resync(ISimpleIteration iteration, EventActor actor, Instant asOf)
    {
        EnsureSameIteration(iteration);

        if (ReplicaWatermark.IsStale(Watermarks.Record, asOf) || !Assign(iteration, actor, asOf))
        {
            return false;
        }

        Watermarks = Watermarks with { Record = asOf };
        return true;
    }

    private bool Assign(ISimpleIteration iteration, EventActor actor, Instant timestamp)
    {
        var previous = (Name, Type, State, DateRange, TeamId);

        Name = iteration.Name;
        Type = iteration.Type;
        State = iteration.State;
        DateRange = iteration.DateRange;
        TeamId = iteration.TeamId;

        if ((Name, Type, State, DateRange, TeamId) == previous)
        {
            return false;
        }

        AddDomainEvent(new WorkIterationUpdatedEvent(this, actor, timestamp));
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
