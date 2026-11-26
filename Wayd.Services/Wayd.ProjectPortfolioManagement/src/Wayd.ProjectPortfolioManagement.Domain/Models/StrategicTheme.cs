using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// The PPM module's copy of a strategic theme, whose primary record lives in the StrategicManagement module.
/// Kept by the <c>StrategicTheme*</c> events (see <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
public sealed class StrategicTheme : IStrategicThemeData, IHasIdAndKey
{
    private StrategicTheme() { }

    /// <param name="strategicTheme">The theme's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public StrategicTheme(IStrategicThemeData strategicTheme, Instant asOf)
    {
        Guard.Against.Null(strategicTheme, nameof(strategicTheme));

        Id = strategicTheme.Id;
        Key = strategicTheme.Key;
        Name = strategicTheme.Name;
        Description = strategicTheme.Description;
        State = strategicTheme.State;
        Watermarks = StrategicThemeWatermarks.At(asOf);
    }

    public Guid Id { get; private init; }

    public int Key { get; private init; }

    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    public StrategicThemeState State { get; private set; }

    public StrategicThemeWatermarks Watermarks { get; private set; } = StrategicThemeWatermarks.None;

    /// <summary>
    /// Whether a project or program may carry this theme. A theme it does not carry yet must be Active; one it
    /// already carries stays whatever its state, so a record tagged with a theme since archived can still be saved.
    /// </summary>
    /// <remarks>
    /// The rule also keeps a deletable theme untagged: Strategic Management deletes only Proposed themes and cannot
    /// see PPM's tags, so a Proposed theme tagged here would make its deletion fail on the tag's foreign key.
    /// </remarks>
    /// <param name="alreadyAttached">Whether the record carries this theme before the change.</param>
    public bool CanBeAttached(bool alreadyAttached) => alreadyAttached || State == StrategicThemeState.Active;

    /// <summary>
    /// Applies a change to the name and description made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDetails(string name, string description, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Details, timestamp))
        {
            return false;
        }

        var before = (Name, Description);

        Name = name;
        Description = description;

        if ((Name, Description) == before && Watermarks.Details == timestamp)
        {
            return false;
        }

        Watermarks = Watermarks with { Details = timestamp };
        return true;
    }

    /// <summary>
    /// Applies a state transition made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyState(StrategicThemeState state, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.State, timestamp)
            || (State == state && Watermarks.State == timestamp))
        {
            return false;
        }

        State = state;
        Watermarks = Watermarks with { State = timestamp };
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
    public bool Resync(IStrategicThemeData strategicTheme, Instant asOf)
    {
        if (strategicTheme.Id != Id)
        {
            throw new InvalidOperationException("Cannot resync StrategicTheme with a different Id.");
        }

        var changed = false;

        if ((Name != strategicTheme.Name.Trim() || Description != strategicTheme.Description.Trim())
            && !ReplicaWatermark.IsStale(Watermarks.Details, asOf))
        {
            Name = strategicTheme.Name;
            Description = strategicTheme.Description;
            Watermarks = Watermarks with { Details = asOf };
            changed = true;
        }

        if (State != strategicTheme.State
            && !ReplicaWatermark.IsStale(Watermarks.State, asOf))
        {
            State = strategicTheme.State;
            Watermarks = Watermarks with { State = asOf };
            changed = true;
        }

        return changed;
    }
}
