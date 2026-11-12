using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A persisted record of linking or unlinking work items and generic links on an incident timeline.
/// </summary>
public sealed class IncidentTimelineLinkEntry : BaseAuditableEntity
{
    private IncidentTimelineLinkEntry() { }

    private IncidentTimelineLinkEntry(
        Guid incidentId,
        IncidentTimelineLinkKind kind,
        IncidentTimelineLinkAction action,
        string label,
        Guid actorEmployeeId,
        Instant recordedAt,
        Guid? targetId,
        Guid? fromTargetId)
    {
        IncidentId = incidentId;
        Kind = kind;
        Action = action;
        Label = label;
        ActorEmployeeId = actorEmployeeId;
        RecordedAt = recordedAt;
        TargetId = targetId;
        FromTargetId = fromTargetId;
    }

    /// <summary>The incident this entry belongs to.</summary>
    public Guid IncidentId { get; private init; }

    /// <summary>Work item vs generic URL link.</summary>
    public IncidentTimelineLinkKind Kind { get; private init; }

    /// <summary>Added, updated, or removed.</summary>
    public IncidentTimelineLinkAction Action { get; private init; }

    /// <summary>Human-readable target (work item title or link name).</summary>
    public string Label
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Label)).Trim();
    } = default!;

    /// <summary>The employee who made the link change.</summary>
    public Guid ActorEmployeeId { get; private init; }

    /// <summary>When the change was recorded on the timeline.</summary>
    public Instant RecordedAt { get; private init; }

    /// <summary>The work item or link this entry is about.</summary>
    public Guid? TargetId { get; private init; }

    /// <summary>The work item or link replaced, when the entry records an update.</summary>
    public Guid? FromTargetId { get; private init; }

    /// <summary>Creates a link change entry for an incident timeline.</summary>
    public static IncidentTimelineLinkEntry Create(
        Guid incidentId,
        IncidentTimelineLinkKind kind,
        IncidentTimelineLinkAction action,
        string label,
        Guid actorEmployeeId,
        Instant recordedAt,
        Guid? targetId,
        Guid? fromTargetId)
    {
        Guard.Against.Default(incidentId, nameof(incidentId));
        Guard.Against.Default(actorEmployeeId, nameof(actorEmployeeId));

        return new IncidentTimelineLinkEntry(
            incidentId,
            kind,
            action,
            label,
            actorEmployeeId,
            recordedAt,
            targetId,
            fromTargetId);
    }
}
