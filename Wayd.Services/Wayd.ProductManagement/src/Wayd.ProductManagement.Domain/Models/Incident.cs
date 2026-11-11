using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement.Incidents;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// An operational incident, linked to one or more products and tracked through a response lifecycle.
/// </summary>
public sealed class Incident : StatusTrackedEntity, IHasIdAndKey
{
    private readonly List<IncidentProductLink> _productLinks = [];
    private readonly List<IncidentReleaseLink> _releaseLinks = [];
    private readonly List<IncidentTimelineNote> _timelineNotes = [];
    private readonly List<IncidentTimelineLinkEntry> _timelineLinkEntries = [];

    private Incident() { }

    private Incident(
        string title,
        string? symptoms,
        string? rootCause,
        Guid commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt)
    {
        Title = title;
        Symptoms = symptoms;
        RootCause = rootCause;
        CommanderEmployeeId = commanderEmployeeId;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;
    }

    /// <inheritdoc/>
    public override string StatusOwnerType => ProductWorkflowOwners.Incident.Key;

    /// <summary>The unique auto-generated key of the incident.</summary>
    public int Key { get; private init; }

    /// <summary>What happened, in a line a responder can scan.</summary>
    public string Title
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Title)).Trim();
    } = default!;

    /// <summary>What responders are seeing. Null when nothing has been recorded yet.</summary>
    public string? Symptoms
    {
        get;
        private set => field = Normalize(value);
    }

    /// <summary>Why it happened, once that is known. Null until a cause is recorded.</summary>
    public string? RootCause
    {
        get;
        private set => field = Normalize(value);
    }

    /// <summary>The employee commanding the response.</summary>
    public Guid CommanderEmployeeId { get; private init; }

    /// <summary>The severity level from the organization's selected scale.</summary>
    public Guid SeverityLevelId { get; private set; }

    /// <summary>When the incident was first noticed.</summary>
    public Instant NoticedAt { get; private set; }

    /// <summary>When active response began, if recorded.</summary>
    public Instant? StartedAt { get; private set; }

    /// <summary>When service was restored, if recorded.</summary>
    public Instant? RecoveredAt { get; private set; }

    /// <summary>The products implicated in the incident.</summary>
    public IReadOnlyCollection<IncidentProductLink> ProductLinks => _productLinks.AsReadOnly();

    /// <summary>The releases this incident points at. One incident may point at several.</summary>
    public IReadOnlyCollection<IncidentReleaseLink> ReleaseLinks => _releaseLinks.AsReadOnly();

    /// <summary>Commander-authored timeline entries.</summary>
    public IReadOnlyCollection<IncidentTimelineNote> TimelineNotes => _timelineNotes.AsReadOnly();

    /// <summary>Link add, update, and remove entries on the operational timeline.</summary>
    public IReadOnlyCollection<IncidentTimelineLinkEntry> TimelineLinkEntries =>
        _timelineLinkEntries.AsReadOnly();

    /// <summary>The well-known meaning of the current status.</summary>
    public ProductStatusAlias StatusAlias => (ProductStatusAlias)StatusAliasValue;

    /// <summary>
    /// Opens a new incident in the <see cref="ProductStatusAlias.Open"/> status.
    /// </summary>
    public static Result<Incident> Create(
        string title,
        string? symptoms,
        string? rootCause,
        Guid commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        IReadOnlyCollection<Guid> productIds,
        StatusRef openStatus,
        EventActor actor,
        Instant timestamp,
        IReadOnlyCollection<Guid>? releaseIds = null)
    {
        Guard.Against.Null(openStatus, nameof(openStatus));

        if (commanderEmployeeId == Guid.Empty)
        {
            return Result.Failure<Incident>("A commander is required.");
        }

        if (severityLevelId == Guid.Empty)
        {
            return Result.Failure<Incident>("A severity level is required.");
        }

        var timeline = ValidateTimeline(noticedAt, startedAt, recoveredAt);
        if (timeline.IsFailure)
        {
            return Result.Failure<Incident>(timeline.Error);
        }

        var incident = new Incident(
            title,
            symptoms,
            rootCause,
            commanderEmployeeId,
            severityLevelId,
            noticedAt,
            startedAt,
            recoveredAt);

        var applied = incident.ApplyStatus(openStatus, actor, timestamp);
        if (applied.IsFailure)
        {
            return Result.Failure<Incident>(applied.Error);
        }

        var products = ReplaceProductLinks(incident, productIds);
        if (products.IsFailure)
        {
            return Result.Failure<Incident>(products.Error);
        }

        var releases = ReplaceReleaseLinks(incident, releaseIds);
        if (releases.IsFailure)
        {
            return Result.Failure<Incident>(releases.Error);
        }

        var createdReleaseIds = incident.ReleaseLinks.Select(link => link.ReleaseId).Order().ToArray();
        var createdSymptoms = incident.Symptoms;
        var createdRootCause = incident.RootCause;

        incident.AddPostPersistenceAction(() =>
        {
            incident.AddDomainEvent(new IncidentCreatedEvent(
                incident.Id,
                incident.Key,
                incident.Title,
                incident.CommanderEmployeeId,
                incident.SeverityLevelId,
                incident.NoticedAt,
                incident.StartedAt,
                incident.RecoveredAt,
                incident.StatusAlias,
                actor,
                timestamp,
                createdSymptoms,
                createdRootCause));

            if (createdReleaseIds.Length > 0)
            {
                incident.AddDomainEvent(new IncidentReleasesChangedEvent(
                    incident.Id,
                    incident.Key,
                    createdReleaseIds,
                    [],
                    createdReleaseIds,
                    actor,
                    timestamp));
            }
        });

        return Result.Success(incident);
    }

    /// <summary>
    /// Updates the editable details of an incident.
    /// </summary>
    public Result UpdateDetails(
        string title,
        string? symptoms,
        string? rootCause,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        IReadOnlyCollection<Guid> productIds,
        EventActor actor,
        Instant timestamp,
        IReadOnlyCollection<Guid>? releaseIds = null)
    {
        if (severityLevelId == Guid.Empty)
        {
            return Result.Failure("A severity level is required.");
        }

        var timeline = ValidateTimeline(noticedAt, startedAt, recoveredAt);
        if (timeline.IsFailure)
        {
            return timeline;
        }

        var previousTitle = Title;
        var previousSymptoms = Symptoms;
        var previousRootCause = RootCause;
        var previousSeverityLevelId = SeverityLevelId;
        var previousNoticedAt = NoticedAt;
        var previousStartedAt = StartedAt;
        var previousRecoveredAt = RecoveredAt;

        Title = title;
        Symptoms = symptoms;
        RootCause = rootCause;
        SeverityLevelId = severityLevelId;
        NoticedAt = noticedAt;
        StartedAt = startedAt;
        RecoveredAt = recoveredAt;

        var products = ReplaceProductLinks(this, productIds);
        if (products.IsFailure)
        {
            return products;
        }

        if (releaseIds is not null)
        {
            var releases = ReplaceReleaseLinks(this, releaseIds);
            if (releases.IsFailure)
            {
                return releases;
            }

            if (releases.Value.Changed)
            {
                AddDomainEvent(new IncidentReleasesChangedEvent(
                    Id,
                    Key,
                    releases.Value.Added,
                    releases.Value.Removed,
                    ReleaseLinks.Select(link => link.ReleaseId).Order().ToArray(),
                    actor,
                    timestamp));
            }
        }

        var detailsChanged =
            previousTitle != Title
            || previousSymptoms != Symptoms
            || previousRootCause != RootCause
            || previousSeverityLevelId != SeverityLevelId;

        if (detailsChanged)
        {
            AddDomainEvent(new IncidentDetailsUpdatedEventV2(
                Id,
                Key,
                previousTitle,
                previousSymptoms,
                previousRootCause,
                previousSeverityLevelId,
                Title,
                Symptoms,
                RootCause,
                SeverityLevelId,
                actor,
                timestamp));
        }

        // A browser date keeps milliseconds and drops the rest of an Instant's
        // ticks, so 17:51:03.1234567 comes back as 17:51:03.123. That round
        // trip is not a change to when the incident was noticed, started, or
        // recovered. A difference of a millisecond or more still is.
        if (!SameMillisecond(previousNoticedAt, NoticedAt)
            || !SameMillisecond(previousStartedAt, StartedAt)
            || !SameMillisecond(previousRecoveredAt, RecoveredAt))
        {
            AddDomainEvent(new IncidentTimelineChangedEvent(
                Id,
                Key,
                previousNoticedAt,
                previousStartedAt,
                previousRecoveredAt,
                NoticedAt,
                StartedAt,
                RecoveredAt,
                actor,
                timestamp));
        }

        return Result.Success();
    }

    /// <summary>Records active response has begun.</summary>
    public Result BeginResponding(
        StatusRef respondingStatus,
        EventActor actor,
        Instant timestamp,
        Instant? responseStartedAt = null)
    {
        var at = responseStartedAt ?? timestamp;
        var statusChange = ChangeStatus(respondingStatus, actor, timestamp);
        if (statusChange.IsFailure)
        {
            return statusChange;
        }

        if (StartedAt is null)
        {
            StartedAt = at;
        }

        return Result.Success();
    }

    /// <summary>Records that service has been restored.</summary>
    public Result MarkRecovered(
        StatusRef recoveredStatus,
        EventActor actor,
        Instant timestamp,
        Instant? recoveredAt = null)
    {
        var at = recoveredAt ?? timestamp;
        var statusChange = ChangeStatus(recoveredStatus, actor, timestamp);
        if (statusChange.IsFailure)
        {
            return statusChange;
        }

        if (RecoveredAt is null)
        {
            RecoveredAt = at;
        }

        if (StartedAt is null)
        {
            StartedAt = at;
        }

        return Result.Success();
    }

    /// <summary>Closes the incident after recovery.</summary>
    public Result Close(StatusRef closedStatus, EventActor actor, Instant timestamp) =>
        ChangeStatus(closedStatus, actor, timestamp);

    /// <summary>Closes the incident without treating it as a real event.</summary>
    public Result Cancel(StatusRef canceledStatus, EventActor actor, Instant timestamp, string? reason = null) =>
        ChangeStatus(canceledStatus, actor, timestamp, reason);

    /// <summary>Records an operational note on the incident timeline.</summary>
    public Result<IncidentTimelineNote> AddTimelineNote(
        string body,
        Guid authorEmployeeId,
        Instant recordedAt,
        EventActor actor,
        Instant timestamp)
    {
        if (authorEmployeeId == Guid.Empty)
        {
            return Result.Failure<IncidentTimelineNote>("An author is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<IncidentTimelineNote>("Timeline note cannot be empty.");
        }

        if (body.Trim().Length > 4096)
        {
            return Result.Failure<IncidentTimelineNote>("Timeline note cannot exceed 4096 characters.");
        }

        var note = IncidentTimelineNote.Create(Id, body, authorEmployeeId, recordedAt);
        _timelineNotes.Add(note);

        AddDomainEvent(new IncidentTimelineNoteAddedEvent(
            Id,
            Key,
            note.Id,
            authorEmployeeId,
            recordedAt,
            actor,
            timestamp));

        return Result.Success(note);
    }

    /// <summary>Records a work item or generic link change on the incident timeline.</summary>
    public Result<IncidentTimelineLinkEntry> RecordLinkTimelineEntry(
        IncidentTimelineLinkKind kind,
        IncidentTimelineLinkAction action,
        string label,
        string? fromLabel,
        string? toLabel,
        EventActor actor,
        Instant recordedAt,
        Instant timestamp)
    {
        if (actor.EmployeeId is null || actor.EmployeeId == Guid.Empty)
        {
            return Result.Failure<IncidentTimelineLinkEntry>("An actor is required.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<IncidentTimelineLinkEntry>("A link label is required.");
        }

        if (label.Trim().Length > 256)
        {
            return Result.Failure<IncidentTimelineLinkEntry>("Link label cannot exceed 256 characters.");
        }

        var actorEmployeeId = actor.EmployeeId.Value;
        var entry = IncidentTimelineLinkEntry.Create(
            Id,
            kind,
            action,
            label,
            actorEmployeeId,
            recordedAt);
        _timelineLinkEntries.Add(entry);

        AddDomainEvent(new IncidentTimelineLinkRecordedEvent(
            Id,
            Key,
            entry.Id,
            kind,
            action,
            label.Trim(),
            fromLabel?.Trim(),
            toLabel?.Trim(),
            recordedAt,
            actor,
            timestamp));

        return Result.Success(entry);
    }

    private Result ChangeStatus(StatusRef status, EventActor actor, Instant timestamp, string? reason = null)
    {
        Guard.Against.Null(status, nameof(status));

        if (status.StatusId == StatusId)
        {
            return Result.Success();
        }

        var fromStatusId = StatusId;
        var fromCategory = StatusCategory;
        var fromAlias = StatusAlias;

        var applied = ApplyStatus(status, actor, timestamp, reason);
        if (applied.IsFailure)
        {
            return applied;
        }

        AddDomainEvent(new IncidentStatusChangedEvent(
            Id,
            Key,
            fromStatusId,
            fromCategory,
            fromAlias,
            status.StatusId,
            status.Category,
            (ProductStatusAlias)status.Alias,
            actor,
            timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Both absent, or both in the same millisecond. Sub-millisecond ticks are
    /// not a different time: a browser date cannot carry them.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool SameMillisecond(Instant? previous, Instant? updated) =>
        previous is null
            ? updated is null
            : updated is not null
                && previous.Value.ToUnixTimeMilliseconds() == updated.Value.ToUnixTimeMilliseconds();

    private static Result ValidateTimeline(Instant noticedAt, Instant? startedAt, Instant? recoveredAt)
    {
        if (startedAt is not null && startedAt.Value < noticedAt)
        {
            return Result.Failure("Response cannot start before the incident was noticed.");
        }

        if (recoveredAt is not null && startedAt is not null && recoveredAt.Value < startedAt.Value)
        {
            return Result.Failure("Recovery cannot be before response started.");
        }

        if (recoveredAt is not null && startedAt is null && recoveredAt.Value < noticedAt)
        {
            return Result.Failure("Recovery cannot be before the incident was noticed.");
        }

        return Result.Success();
    }

    /// <summary>
    /// Replaces the release set. A null list leaves it unchanged so callers that do not mention
    /// releases keep whatever is already linked. An empty list clears it.
    /// </summary>
    private static Result<(bool Changed, Guid[] Added, Guid[] Removed)> ReplaceReleaseLinks(
        Incident incident,
        IReadOnlyCollection<Guid>? releaseIds)
    {
        var next = (releaseIds ?? []).Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var current = incident._releaseLinks.Select(link => link.ReleaseId).ToHashSet();
        var added = next.Except(current).Order().ToArray();
        var removed = current.Except(next).Order().ToArray();
        if (added.Length == 0 && removed.Length == 0)
        {
            return Result.Success((false, added, removed));
        }

        incident._releaseLinks.Clear();
        foreach (var releaseId in next.Order())
        {
            incident._releaseLinks.Add(new IncidentReleaseLink(incident.Id, releaseId));
        }

        return Result.Success((true, added, removed));
    }

    private static Result ReplaceProductLinks(Incident incident, IReadOnlyCollection<Guid> productIds)
    {
        var distinct = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        incident._productLinks.Clear();

        foreach (var productId in distinct)
        {
            incident._productLinks.Add(new IncidentProductLink(incident.Id, productId));
        }

        return Result.Success();
    }
}
