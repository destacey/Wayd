using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

/// <summary>
/// The Planning module's copy of a sprint, kept by the <c>Iteration*</c> events (see
/// <see cref="ReplicaWatermark"/> for how it orders them). It holds every iteration the source holds, whatever its
/// <see cref="Type"/>, because an iteration node moves between the two types when its team's type changes; readers
/// that want sprints filter on <see cref="IterationType.Sprint"/>.
/// </summary>
public sealed class PlanningSprint : ISimpleIteration, IHasIdAndKey
{
    private PlanningSprint() { }

    /// <param name="sprint">The sprint's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public PlanningSprint(ISimpleIteration sprint, Instant asOf)
    {
        Id = sprint.Id;
        Key = sprint.Key;
        Name = sprint.Name;
        Type = sprint.Type;
        DateRange = sprint.DateRange;
        TeamId = sprint.TeamId;
        Watermarks = PlanningSprintWatermarks.At(asOf);
    }

    public Guid Id { get; private set; }
    public int Key { get; private set; }
    public string Name { get; private set; } = default!;
    public IterationType Type { get; private set; }
    public IterationDateRange DateRange { get; private set; } = default!;
    public Guid? TeamId { get; private set; }
    public PlanningTeam? Team { get; private set; }
    public PlanningSprintWatermarks Watermarks { get; private set; } = PlanningSprintWatermarks.None;

    /// <summary>Applies a change to the name and type made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDetails(string name, IterationType type, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Details, timestamp)
            || (Name == name && Type == type && Watermarks.Details == timestamp))
        {
            return false;
        }

        Name = name;
        Type = type;
        Watermarks = Watermarks with { Details = timestamp };
        return true;
    }

    /// <summary>Applies a change to the date range made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDateRange(IterationDateRange dateRange, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.DateRange, timestamp)
            || (DateRange == dateRange && Watermarks.DateRange == timestamp))
        {
            return false;
        }

        DateRange = dateRange;
        Watermarks = Watermarks with { DateRange = timestamp };
        return true;
    }

    /// <summary>Applies a change of team made at <paramref name="timestamp"/>.</summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyTeam(Guid? teamId, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Team, timestamp)
            || (TeamId == teamId && Watermarks.Team == timestamp))
        {
            return false;
        }

        TeamId = teamId;
        Watermarks = Watermarks with { Team = timestamp };
        return true;
    }

    /// <summary>
    /// Applies the whole sprint as a change made at <paramref name="timestamp"/> left it, one group at a time.
    /// Only the superseded whole-record event needs this.
    /// </summary>
    /// <returns>Whether any group applied.</returns>
    public bool ApplyRecord(ISimpleIteration sprint, Instant timestamp)
    {
        EnsureSameSprint(sprint);

        var details = ApplyDetails(sprint.Name, sprint.Type, timestamp);
        var dateRange = ApplyDateRange(sprint.DateRange, timestamp);
        var team = ApplyTeam(sprint.TeamId, timestamp);

        return details || dateRange || team;
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
    public bool Resync(ISimpleIteration sprint, Instant asOf)
    {
        EnsureSameSprint(sprint);

        var changed = false;

        if ((Name != sprint.Name || Type != sprint.Type) && !ReplicaWatermark.IsStale(Watermarks.Details, asOf))
        {
            Name = sprint.Name;
            Type = sprint.Type;
            Watermarks = Watermarks with { Details = asOf };
            changed = true;
        }

        if (DateRange != sprint.DateRange && !ReplicaWatermark.IsStale(Watermarks.DateRange, asOf))
        {
            DateRange = sprint.DateRange;
            Watermarks = Watermarks with { DateRange = asOf };
            changed = true;
        }

        if (TeamId != sprint.TeamId && !ReplicaWatermark.IsStale(Watermarks.Team, asOf))
        {
            TeamId = sprint.TeamId;
            Watermarks = Watermarks with { Team = asOf };
            changed = true;
        }

        return changed;
    }

    private void EnsureSameSprint(ISimpleIteration sprint)
    {
        if (sprint.Id != Id)
        {
            throw new InvalidOperationException("Cannot apply a different sprint to this PlanningSprint.");
        }
    }
}
