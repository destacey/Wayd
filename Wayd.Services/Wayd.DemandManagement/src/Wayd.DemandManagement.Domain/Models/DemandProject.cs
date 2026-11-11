using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Replication;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// Demand Management's copy of a PPM project, sharing its id: what an outcome is checked against and named
/// by. Kept by the <c>Project*</c> events (see <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
/// <remarks>
/// Holds only what the module shows: the key and name, and the lifecycle category its outcomes report their
/// delivery from. Nothing here decides who may see the project; the request an outcome belongs to decides who
/// may see that the project came from it.
/// </remarks>
public sealed class DemandProject
{
    private DemandProject() { }

    /// <param name="project">The project's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public DemandProject(ISimpleProjectWithLifecycle project, Instant asOf)
    {
        Id = project.Id;
        Key = project.Key;
        Name = project.Name;
        LifecycleCategory = project.LifecycleCategory;
        Watermarks = DemandProjectWatermarks.At(asOf);
    }

    /// <summary>The project's id, the same as in PPM.</summary>
    public Guid Id { get; private set; }

    /// <summary>The project's key, such as <c>APOLLO</c>.</summary>
    public ProjectKey Key { get; private set; } = default!;

    /// <summary>The project's name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Where the project's status stands in its lifecycle: the category, never the status, since statuses are
    /// on their way to being workflow-configurable.
    /// </summary>
    public LifecycleCategory LifecycleCategory { get; private set; }

    /// <summary>The delivery the project's lifecycle reports, for an outcome that follows the project as a whole.</summary>
    public DeliverySignal DeliverySignal => DeliverySignals.Of(LifecycleCategory);

    /// <summary>When each group of fields last took a change.</summary>
    public DemandProjectWatermarks Watermarks { get; private set; } = DemandProjectWatermarks.None;

    /// <summary>
    /// Applies a change to the name made at <paramref name="timestamp"/>.
    /// </summary>
    /// <remarks>
    /// Never writes the key, although the details event carries one: the key has its own event, and a details
    /// change delivered after a rekey would otherwise put the old key back.
    /// </remarks>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyName(string name, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Name, timestamp)
            || (Name == name && Watermarks.Name == timestamp))
        {
            return false;
        }

        Name = name;
        Watermarks = Watermarks with { Name = timestamp };
        return true;
    }

    /// <summary>
    /// Applies a key change made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyKey(ProjectKey key, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Key, timestamp)
            || (Equals(Key, key) && Watermarks.Key == timestamp))
        {
            return false;
        }

        Key = key;
        Watermarks = Watermarks with { Key = timestamp };
        return true;
    }

    /// <summary>
    /// Applies a move to a status in <paramref name="category"/> made at <paramref name="timestamp"/>.
    /// </summary>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyLifecycle(LifecycleCategory category, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Lifecycle, timestamp)
            || (LifecycleCategory == category && Watermarks.Lifecycle == timestamp))
        {
            return false;
        }

        LifecycleCategory = category;
        Watermarks = Watermarks with { Lifecycle = timestamp };
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
    public bool Resync(ISimpleProjectWithLifecycle project, Instant asOf)
    {
        if (project.Id != Id)
            throw new InvalidOperationException("Cannot resync a DemandProject with a different Id.");

        var changed = false;

        if (Name != project.Name && !ReplicaWatermark.IsStale(Watermarks.Name, asOf))
        {
            Name = project.Name;
            Watermarks = Watermarks with { Name = asOf };
            changed = true;
        }

        if (!Equals(Key, project.Key) && !ReplicaWatermark.IsStale(Watermarks.Key, asOf))
        {
            Key = project.Key;
            Watermarks = Watermarks with { Key = asOf };
            changed = true;
        }

        if (LifecycleCategory != project.LifecycleCategory && !ReplicaWatermark.IsStale(Watermarks.Lifecycle, asOf))
        {
            LifecycleCategory = project.LifecycleCategory;
            Watermarks = Watermarks with { Lifecycle = asOf };
            changed = true;
        }

        return changed;
    }
}
