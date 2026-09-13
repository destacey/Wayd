using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.Replication;
using NodaTime;

namespace Wayd.Work.Domain.Models;

/// <summary>
/// The Work module's copy of a PPM project, kept by the <c>Project*</c> events (see
/// <see cref="ReplicaWatermark"/> for how it orders them).
/// </summary>
public sealed class WorkProject : ISimpleProject, IHasIdAndKey<ProjectKey>
{
    private WorkProject() { }

    /// <param name="project">The project's state as read from its source.</param>
    /// <param name="asOf">
    /// The timestamp of the change that led to the copy being created. Every group is stamped with it, so an
    /// older event still in flight is skipped rather than rolling back state the source already moved past.
    /// </param>
    public WorkProject(ISimpleProject project, Instant asOf)
    {
        Id = project.Id;
        Key = project.Key;
        Name = project.Name;
        Description = project.Description;
        Watermarks = WorkProjectWatermarks.At(asOf);
    }

    public Guid Id { get; private set; }
    public ProjectKey Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public WorkProjectWatermarks Watermarks { get; private set; } = WorkProjectWatermarks.None;

    /// <summary>
    /// Applies a change to the name and description made at <paramref name="timestamp"/>.
    /// </summary>
    /// <remarks>
    /// Never writes the key, although the details event carries one: the key has its own event, and a details
    /// change delivered after a rekey would otherwise put the old key back.
    /// </remarks>
    /// <returns>False when a newer change already applied, or when the copy already holds this one.</returns>
    public bool ApplyDetails(string name, string description, Instant timestamp)
    {
        if (ReplicaWatermark.IsStale(Watermarks.Details, timestamp)
            || (Name == name && Description == description && Watermarks.Details == timestamp))
        {
            return false;
        }

        Name = name;
        Description = description;
        Watermarks = Watermarks with { Details = timestamp };
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
    /// Brings the copy in line with the source as read at <paramref name="asOf"/>, one group at a time.
    /// </summary>
    /// <remarks>
    /// A group that already matches keeps its watermark: stamping it would skip a change the read could not
    /// see, one timestamped before <paramref name="asOf"/> but committed after it. A group holding a change
    /// newer than the read is left alone too, because the read is the older of the two.
    /// </remarks>
    /// <returns>Whether anything changed.</returns>
    public bool Resync(ISimpleProject project, Instant asOf)
    {
        if (project.Id != Id)
        {
            throw new InvalidOperationException("Cannot resync WorkProject with a different Id.");
        }

        var changed = false;

        if ((Name != project.Name || Description != project.Description)
            && !ReplicaWatermark.IsStale(Watermarks.Details, asOf))
        {
            Name = project.Name;
            Description = project.Description;
            Watermarks = Watermarks with { Details = asOf };
            changed = true;
        }

        if (!Equals(Key, project.Key)
            && !ReplicaWatermark.IsStale(Watermarks.Key, asOf))
        {
            Key = project.Key;
            Watermarks = Watermarks with { Key = asOf };
            changed = true;
        }

        return changed;
    }
}
