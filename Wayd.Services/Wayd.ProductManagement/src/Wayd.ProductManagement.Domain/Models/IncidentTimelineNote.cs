using Ardalis.GuardClauses;
using NodaTime;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A commander-authored entry on an incident's operational timeline.
/// </summary>
public sealed class IncidentTimelineNote : BaseAuditableEntity
{
    private IncidentTimelineNote() { }

    private IncidentTimelineNote(
        Guid incidentId,
        string body,
        Guid authorEmployeeId,
        Instant recordedAt)
    {
        IncidentId = incidentId;
        Body = body;
        AuthorEmployeeId = authorEmployeeId;
        RecordedAt = recordedAt;
    }

    /// <summary>The incident this note belongs to.</summary>
    public Guid IncidentId { get; private init; }

    /// <summary>What happened or what responders should know.</summary>
    public string Body
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Body)).Trim();
    } = default!;

    /// <summary>The employee who recorded the note.</summary>
    public Guid AuthorEmployeeId { get; private init; }

    /// <summary>When the event occurred or was recorded on the timeline.</summary>
    public Instant RecordedAt { get; private init; }

    /// <summary>Creates a note for an incident timeline.</summary>
    public static IncidentTimelineNote Create(
        Guid incidentId,
        string body,
        Guid authorEmployeeId,
        Instant recordedAt)
    {
        Guard.Against.Default(incidentId, nameof(incidentId));
        Guard.Against.Default(authorEmployeeId, nameof(authorEmployeeId));

        return new IncidentTimelineNote(incidentId, body, authorEmployeeId, recordedAt);
    }
}
