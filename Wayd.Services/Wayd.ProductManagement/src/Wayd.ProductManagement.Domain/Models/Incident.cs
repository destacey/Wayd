using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Integrations;
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
        string? remediation,
        string? rootCause,
        Guid submittedByEmployeeId,
        Guid reportedByEmployeeId,
        Guid? commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt)
    {
        Title = title;
        Symptoms = symptoms;
        Remediation = remediation;
        RootCause = rootCause;
        SubmittedByEmployeeId = submittedByEmployeeId;
        ReportedByEmployeeId = reportedByEmployeeId;
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

    /// <summary>What was done to restore service or stop the impact. Null until a remediation is recorded.</summary>
    public string? Remediation
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

    /// <summary>The employee who opened the incident. Set once, when it is created.</summary>
    public Guid SubmittedByEmployeeId { get; private init; }

    /// <summary>The employee who reported the incident.</summary>
    public Guid ReportedByEmployeeId { get; private set; }

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public Guid? CommanderEmployeeId { get; private set; }

    /// <summary>The severity level from the organization's selected scale.</summary>
    public Guid SeverityLevelId { get; private set; }

    /// <summary>When the incident was first noticed.</summary>
    public Instant NoticedAt { get; private set; }

    /// <summary>When active response began, if recorded.</summary>
    public Instant? StartedAt { get; private set; }

    /// <summary>When service was restored, if recorded.</summary>
    public Instant? RecoveredAt { get; private set; }

    /// <summary>
    /// The one Teams chat stored on the incident. Null when none is.
    /// The same <see cref="RecordIntegration"/> shape can hold a link to another system.
    /// </summary>
    public RecordIntegration? TeamsChat { get; private set; }

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
        Guid? commanderEmployeeId,
        Guid severityLevelId,
        Instant noticedAt,
        Instant? startedAt,
        Instant? recoveredAt,
        IReadOnlyCollection<Guid> productIds,
        StatusRef openStatus,
        EventActor actor,
        Instant timestamp,
        Guid submittedByEmployeeId,
        Guid reportedByEmployeeId,
        IReadOnlyCollection<Guid>? releaseIds = null,
        string? remediation = null)
    {
        Guard.Against.Null(openStatus, nameof(openStatus));

        if (submittedByEmployeeId == Guid.Empty)
        {
            return Result.Failure<Incident>("Who opened the incident is required.");
        }

        if (reportedByEmployeeId == Guid.Empty)
        {
            return Result.Failure<Incident>("Who reported the incident is required.");
        }

        var commander = commanderEmployeeId is null || commanderEmployeeId == Guid.Empty
            ? null
            : commanderEmployeeId;

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
            remediation,
            rootCause,
            submittedByEmployeeId,
            reportedByEmployeeId,
            commander,
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

        var createdTitle = incident.Title;
        var createdSubmittedByEmployeeId = incident.SubmittedByEmployeeId;
        var createdReportedByEmployeeId = incident.ReportedByEmployeeId;
        var createdCommanderEmployeeId = incident.CommanderEmployeeId;
        var createdSeverityLevelId = incident.SeverityLevelId;
        var createdNoticedAt = incident.NoticedAt;
        var createdStartedAt = incident.StartedAt;
        var createdRecoveredAt = incident.RecoveredAt;
        var createdStatusAlias = incident.StatusAlias;
        var createdSymptoms = incident.Symptoms;
        var createdRemediation = incident.Remediation;
        var createdRootCause = incident.RootCause;
        var createdProductIds = incident.ProductLinks.Select(link => link.ProductId).Order().ToArray();
        var createdReleaseIds = incident.ReleaseLinks.Select(link => link.ReleaseId).Order().ToArray();

        incident.AddPostPersistenceAction(() =>
            incident.AddDomainEvent(new IncidentCreatedEvent(
                incident.Id,
                incident.Key,
                createdTitle,
                createdSubmittedByEmployeeId,
                createdReportedByEmployeeId,
                createdCommanderEmployeeId,
                createdSeverityLevelId,
                createdNoticedAt,
                createdStartedAt,
                createdRecoveredAt,
                createdStatusAlias,
                actor,
                timestamp,
                createdSymptoms,
                createdRemediation,
                createdRootCause,
                createdProductIds,
                createdReleaseIds)));

        return Result.Success(incident);
    }

    /// <summary>
    /// Updates the editable details of an incident.
    /// </summary>
    /// <remarks>
    /// Who reported it changes only when <paramref name="reportedByEmployeeId"/> is set.
    /// The commander changes only when <paramref name="setCommander"/> is true, and a null or empty
    /// commander then clears the assignment. Who opened the incident is not editable.
    /// </remarks>
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
        IReadOnlyCollection<Guid>? releaseIds = null,
        Guid? reportedByEmployeeId = null,
        bool setCommander = false,
        Guid? commanderEmployeeId = null,
        string? remediation = null)
    {
        if (severityLevelId == Guid.Empty)
        {
            return Result.Failure("A severity level is required.");
        }

        if (reportedByEmployeeId == Guid.Empty)
        {
            return Result.Failure("Who reported the incident is required.");
        }

        var timeline = ValidateTimeline(noticedAt, startedAt, recoveredAt);
        if (timeline.IsFailure)
        {
            return timeline;
        }

        var previousTitle = Title;
        var previousSymptoms = Symptoms;
        var previousRemediation = Remediation;
        var previousRootCause = RootCause;
        var previousSeverityLevelId = SeverityLevelId;
        var previousNoticedAt = NoticedAt;
        var previousStartedAt = StartedAt;
        var previousRecoveredAt = RecoveredAt;

        Title = title;
        Symptoms = symptoms;
        Remediation = remediation;
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

        if (products.Value.Changed)
        {
            AddDomainEvent(new IncidentProductsChangedEvent(
                Id,
                Key,
                products.Value.Added,
                products.Value.Removed,
                ProductLinks.Select(link => link.ProductId).Order().ToArray(),
                actor,
                timestamp));
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
            || previousRemediation != Remediation
            || previousRootCause != RootCause
            || previousSeverityLevelId != SeverityLevelId;

        if (detailsChanged)
        {
            AddDomainEvent(new IncidentDetailsUpdatedEvent(
                Id,
                Key,
                previousTitle,
                previousSymptoms,
                previousRemediation,
                previousRootCause,
                previousSeverityLevelId,
                Title,
                Symptoms,
                Remediation,
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

        if (reportedByEmployeeId is Guid nextReportedBy && nextReportedBy != ReportedByEmployeeId)
        {
            var previousReportedBy = ReportedByEmployeeId;
            ReportedByEmployeeId = nextReportedBy;
            AddDomainEvent(new IncidentReportedByChangedEvent(
                Id,
                Key,
                previousReportedBy,
                ReportedByEmployeeId,
                actor,
                timestamp));
        }

        if (setCommander)
        {
            var nextCommander = commanderEmployeeId is null || commanderEmployeeId == Guid.Empty
                ? null
                : commanderEmployeeId;
            if (nextCommander != CommanderEmployeeId)
            {
                var previousCommander = CommanderEmployeeId;
                CommanderEmployeeId = nextCommander;
                AddDomainEvent(new IncidentCommanderChangedEvent(
                    Id,
                    Key,
                    previousCommander,
                    CommanderEmployeeId,
                    actor,
                    timestamp));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Stores one pasted Teams link on the incident, or removes the stored chat when the link is empty.
    /// Wayd does not join the chat and does not look it up. A pasted link is not a chat Wayd created.
    /// </summary>
    public Result LinkTeamsChat(string? link, string? displayName, EventActor actor, Instant timestamp)
    {
        string? normalizedLink;
        string? normalizedName;
        if (string.IsNullOrWhiteSpace(link))
        {
            normalizedLink = null;
            normalizedName = null;
        }
        else if (!TryNormalizeHttpsLink(link, out var httpsLink))
        {
            return Result.Failure("A Teams chat link must be an https URL.");
        }
            else if (httpsLink.Length > RecordIntegration.LinkMaxLength)
            {
                return Result.Failure("A Teams chat link cannot be longer than 2048 characters.");
            }
            else
            {
                normalizedLink = httpsLink;
                normalizedName = Normalize(displayName);
                if (normalizedName is not null && normalizedName.Length > RecordIntegration.TitleMaxLength)
                {
                    return Result.Failure("A Teams chat name cannot be longer than 256 characters.");
                }
            }

        var next = normalizedLink is null
            ? null
            : RecordIntegration.Create(Connector.Teams, null, normalizedName, normalizedLink, createdByWayd: false);
        if (SameIntegration(TeamsChat, next))
        {
            return Result.Success();
        }

        var previous = TeamsChat;
        TeamsChat = next;

        AddDomainEvent(new IncidentTeamsChatLinkedEvent(
            Id,
            Key,
            previous?.ExternalId,
            previous?.Title,
            previous?.Link,
            previous?.CreatedByWayd ?? false,
            TeamsChat?.ExternalId,
            TeamsChat?.Title,
            TeamsChat?.Link,
            TeamsChat?.CreatedByWayd ?? false,
            actor,
            timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Stores the one Teams chat Wayd created for this incident. Fails when a chat is already stored,
    /// so a second create cannot replace the first. A pasted link is a different method and can replace it.
    /// </summary>
    public Result RecordCreatedTeamsChat(
        string? externalId,
        string? title,
        string? link,
        EventActor actor,
        Instant timestamp)
    {
        if (TeamsChat is not null)
            return Result.Failure("This incident already has a Teams chat.");

        var id = externalId?.Trim();
        if (string.IsNullOrEmpty(id))
            return Result.Failure("A Teams chat id is required.");
        if (id.Length > RecordIntegration.ExternalIdMaxLength)
            return Result.Failure("A Teams chat id cannot be longer than 512 characters.");

        var name = Normalize(title);
        if (name is null)
            return Result.Failure("A Teams chat name is required.");
        if (name.Length > RecordIntegration.TitleMaxLength)
            return Result.Failure("A Teams chat name cannot be longer than 256 characters.");

        if (!TryNormalizeHttpsLink(link, out var httpsLink))
            return Result.Failure("A Teams chat link must be an https URL.");
        if (httpsLink.Length > RecordIntegration.LinkMaxLength)
            return Result.Failure("A Teams chat link cannot be longer than 2048 characters.");

        TeamsChat = RecordIntegration.Create(Connector.Teams, id, name, httpsLink, createdByWayd: true);
        AddDomainEvent(new IncidentTeamsChatCreatedEvent(
            Id,
            Key,
            fromTeamsChatId: null,
            fromTeamsChatTitle: null,
            fromTeamsChatLink: null,
            fromTeamsChatCreatedByWayd: false,
            TeamsChat.ExternalId,
            TeamsChat.Title,
            TeamsChat.Link,
            TeamsChat.CreatedByWayd,
            actor,
            timestamp));

        return Result.Success();
    }

    private static bool SameIntegration(RecordIntegration? left, RecordIntegration? right) =>
        left?.Connector == right?.Connector
        && left?.ExternalId == right?.ExternalId
        && left?.Title == right?.Title
        && left?.Link == right?.Link
        && (left?.CreatedByWayd ?? false) == (right?.CreatedByWayd ?? false);

    /// <summary>Records active response has begun.</summary>
    public Result BeginResponding(
        StatusRef respondingStatus,
        EventActor actor,
        Instant timestamp,
        Instant? responseStartedAt = null)
    {
        var at = responseStartedAt ?? timestamp;
        var nextStarted = StartedAt ?? at;
        var timeline = ValidateTimeline(NoticedAt, nextStarted, RecoveredAt);
        if (timeline.IsFailure)
        {
            return timeline;
        }

        return ChangeStatus(respondingStatus, actor, timestamp, nextStartedAt: nextStarted);
    }

    /// <summary>Records that service has been restored.</summary>
    public Result MarkRecovered(
        StatusRef recoveredStatus,
        EventActor actor,
        Instant timestamp,
        Instant? recoveredAt = null)
    {
        var at = recoveredAt ?? timestamp;
        var nextStarted = StartedAt ?? at;
        var nextRecovered = RecoveredAt ?? at;
        var timeline = ValidateTimeline(NoticedAt, nextStarted, nextRecovered);
        if (timeline.IsFailure)
        {
            return timeline;
        }

        return ChangeStatus(
            recoveredStatus,
            actor,
            timestamp,
            nextStartedAt: nextStarted,
            nextRecoveredAt: nextRecovered);
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
        Instant timestamp,
        Guid? targetId = null,
        Guid? fromTargetId = null)
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
            recordedAt,
            targetId,
            fromTargetId);
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
            timestamp,
            targetId,
            fromTargetId));

        return Result.Success(entry);
    }

    private Result ChangeStatus(
        StatusRef status,
        EventActor actor,
        Instant timestamp,
        string? reason = null,
        Instant? nextStartedAt = null,
        Instant? nextRecoveredAt = null)
    {
        Guard.Against.Null(status, nameof(status));

        var fromStartedAt = StartedAt;
        var fromRecoveredAt = RecoveredAt;
        var toStartedAt = nextStartedAt ?? StartedAt;
        var toRecoveredAt = nextRecoveredAt ?? RecoveredAt;
        var statusSame = status.StatusId == StatusId;
        var datesSame = SameMillisecond(fromStartedAt, toStartedAt)
            && SameMillisecond(fromRecoveredAt, toRecoveredAt);

        if (statusSame && datesSame)
        {
            return Result.Success();
        }

        var fromStatusId = StatusId;
        var fromCategory = StatusCategory;
        var fromAlias = StatusAlias;

        if (!statusSame)
        {
            var applied = ApplyStatus(status, actor, timestamp, reason);
            if (applied.IsFailure)
            {
                return applied;
            }
        }

        if (!datesSame)
        {
            StartedAt = toStartedAt;
            RecoveredAt = toRecoveredAt;
        }

        if (statusSame)
        {
            AddDomainEvent(new IncidentTimelineChangedEvent(
                Id,
                Key,
                NoticedAt,
                fromStartedAt,
                fromRecoveredAt,
                NoticedAt,
                StartedAt,
                RecoveredAt,
                actor,
                timestamp));

            return Result.Success();
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
            timestamp,
            fromStartedAt,
            StartedAt,
            fromRecoveredAt,
            RecoveredAt));

        return Result.Success();
    }

    /// <summary>
    /// Both absent, or both in the same millisecond. Sub-millisecond ticks are
    /// not a different time: a browser date cannot carry them.
    /// </summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryNormalizeHttpsLink(string? link, out string normalized)
    {
        normalized = link?.Trim() ?? "";
        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

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

    private static Result<(bool Changed, Guid[] Added, Guid[] Removed)> ReplaceProductLinks(
        Incident incident,
        IReadOnlyCollection<Guid> productIds)
    {
        var next = productIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var current = incident._productLinks.Select(link => link.ProductId).ToHashSet();
        var added = next.Except(current).Order().ToArray();
        var removed = current.Except(next).Order().ToArray();
        if (added.Length == 0 && removed.Length == 0)
        {
            return Result.Success((false, added, removed));
        }

        incident._productLinks.Clear();
        foreach (var productId in next.Order())
        {
            incident._productLinks.Add(new IncidentProductLink(incident.Id, productId));
        }

        return Result.Success((true, added, removed));
    }
}
