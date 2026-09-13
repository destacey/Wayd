using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Replication;
using Wayd.Planning.Domain.Models.Iterations;
using NodaTime;

namespace Wayd.Planning.Domain.Models;

/// <summary>
/// The Planning module's copy of an Organization team, kept by the <c>Team*</c> events (see
/// <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
public sealed class PlanningTeam : ISimpleTeam, IHasIdAndKey, IHasTeamIdAndCode
{
    private readonly List<Iteration> _iterations = [];
    private readonly List<PlanningIntervalTeam> _planningIntervalTeams = [];

    private PlanningTeam() { }

    /// <param name="team">The team's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public PlanningTeam(ISimpleTeam team, Instant asOf)
    {
        Id = team.Id;
        Key = team.Key;
        Name = team.Name;
        Code = team.Code;
        Type = team.Type;
        IsActive = team.IsActive;
        Watermarks = TeamReplicaWatermarks.At(asOf);
    }

    public Guid Id { get; private set; }
    public int Key { get; private set; }
    public string Name { get; private set; } = default!;
    public TeamCode Code { get; private set; } = default!;
    public TeamType Type { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public TeamReplicaWatermarks Watermarks { get; private set; } = TeamReplicaWatermarks.None;
    public IReadOnlyCollection<Iteration> Iterations => _iterations.AsReadOnly();
    public IReadOnlyCollection<PlanningIntervalTeam> PlanningIntervalTeams => _planningIntervalTeams.AsReadOnly();

    /// <summary>
    /// Applies a change to the name and code made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDetails(string name, TeamCode code, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Details, timestamp)
            || (Name == name && Equals(Code, code) && Watermarks.Details == timestamp))
        {
            return false;
        }

        Name = name;
        Code = code;
        Watermarks = Watermarks with { Details = timestamp };
        return true;
    }

    /// <summary>
    /// Applies an activation or deactivation made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyActivation(bool isActive, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Activation, timestamp)
            || (IsActive == isActive && Watermarks.Activation == timestamp))
        {
            return false;
        }

        IsActive = isActive;
        Watermarks = Watermarks with { Activation = timestamp };
        return true;
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
    public bool Resync(ISimpleTeam team, Instant asOf)
    {
        if (Id != team.Id)
        {
            throw new InvalidOperationException("Cannot resync PlanningTeam with a different Id.");
        }

        var changed = false;

        if ((Name != team.Name || !Equals(Code, team.Code))
            && !ReplicaWatermark.IsStale(Watermarks.Details, asOf))
        {
            Name = team.Name;
            Code = team.Code;
            Watermarks = Watermarks with { Details = asOf };
            changed = true;
        }

        if (IsActive != team.IsActive
            && !ReplicaWatermark.IsStale(Watermarks.Activation, asOf))
        {
            IsActive = team.IsActive;
            Watermarks = Watermarks with { Activation = asOf };
            changed = true;
        }

        return changed;
    }
}
