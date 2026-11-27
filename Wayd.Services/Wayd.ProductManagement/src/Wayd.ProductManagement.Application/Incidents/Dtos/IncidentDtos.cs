using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Dtos;

/// <summary>Severity level shown on incident pages.</summary>
public sealed record IncidentSeverityLevelNavigationDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int SortOrder { get; init; }

    /// <summary>Hex accent color (e.g. "#E07A4C").</summary>
    public required string Color { get; init; }
}

/// <summary>
/// One incident on a list page.
/// </summary>
public sealed record IncidentSummaryDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Title { get; init; } = default!;
    public Instant NoticedAt { get; init; }
    public Instant? StartedAt { get; init; }
    public Instant? RecoveredAt { get; init; }

    /// <summary>The employee who opened the incident.</summary>
    public EmployeeNavigationDto SubmittedBy { get; init; } = default!;

    /// <summary>The employee who reported the incident.</summary>
    public EmployeeNavigationDto ReportedBy { get; init; } = default!;

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public EmployeeNavigationDto? Commander { get; init; }
    public IncidentSeverityLevelNavigationDto SeverityLevel { get; init; } = default!;
    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }
    public IReadOnlyList<NavigationDto> Products { get; init; } = [];

    /// <summary>Releases this incident points at. One incident may point at several.</summary>
    public IReadOnlyList<NavigationDto> Releases { get; init; } = [];
}

/// <summary>An active incident a product or release row links to.</summary>
public sealed record ActiveIncidentLinkDto
{
    /// <summary>Incident key. The record is addressed by this key.</summary>
    public int Key { get; init; }

    /// <summary>Incident title, the name in the hover list.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Severity name shown beside the title.</summary>
    public string SeverityName { get; init; } = string.Empty;

    /// <summary>Severity color, the accent beside the severity name.</summary>
    public string SeverityColor { get; init; } = string.Empty;

    /// <summary>The incident's current status name.</summary>
    public string StatusName { get; init; } = string.Empty;
}

/// <summary>An active incident on a Proposed or Active product one of the caller's teams is linked to.</summary>
public sealed record TeamProductIncidentDto
{
    /// <summary>Incident id.</summary>
    public Guid Id { get; init; }

    /// <summary>Incident key. The record is addressed by this key.</summary>
    public int Key { get; init; }

    /// <summary>What went wrong, as the incident is titled.</summary>
    public string Title { get; init; } = default!;

    /// <summary>When the incident was first noticed; how long it has been active runs from here.</summary>
    public Instant NoticedAt { get; init; }

    /// <summary>How severe the incident is; the lowest sort order is the most severe.</summary>
    public IncidentSeverityLevelNavigationDto SeverityLevel { get; init; } = default!;

    /// <summary>
    /// Where the incident is in its workflow: any status not aliased Closed or Canceled and not in a Done or Removed
    /// category.
    /// </summary>
    public StatusNavigationDto Status { get; init; } = default!;

    /// <summary>
    /// The Proposed or Active products the incident is on that the caller's teams are linked to, by name. The incident may be on
    /// other products too; those are on its record.
    /// </summary>
    public IReadOnlyList<NavigationDto> Products { get; init; } = [];
}

/// <summary>
/// One incident in full.
/// </summary>
public sealed record IncidentDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Title { get; init; } = default!;

    /// <summary>What responders are seeing. Null when nothing has been recorded yet.</summary>
    public string? Symptoms { get; init; }

    /// <summary>What was done to restore service or stop the impact. Null until a remediation is recorded.</summary>
    public string? Remediation { get; init; }

    /// <summary>Why it happened, once that is known. Null until a cause is recorded.</summary>
    public string? RootCause { get; init; }

    /// <summary>The employee who opened the incident.</summary>
    public EmployeeNavigationDto SubmittedBy { get; init; } = default!;

    /// <summary>The employee who reported the incident.</summary>
    public EmployeeNavigationDto ReportedBy { get; init; } = default!;

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public EmployeeNavigationDto? Commander { get; init; }
    public IncidentSeverityLevelNavigationDto SeverityLevel { get; init; } = default!;
    public Instant NoticedAt { get; init; }
    public Instant? StartedAt { get; init; }
    public Instant? RecoveredAt { get; init; }
    public IReadOnlyList<NavigationDto> Products { get; init; } = [];

    /// <summary>Releases this incident points at. One incident may point at several.</summary>
    public IReadOnlyList<NavigationDto> Releases { get; init; } = [];

    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }

    /// <summary>The Teams chat id, when Wayd created the chat. A pasted link has none.</summary>
    public string? TeamsChatId { get; init; }

    /// <summary>The name shown for the incident's Teams chat.</summary>
    public string? TeamsChatDisplayName { get; init; }

    /// <summary>The link that opens the incident's one Teams chat. Null when none is stored.</summary>
    public string? TeamsChatLink { get; init; }

    /// <summary>True only when Wayd created the stored chat.</summary>
    public bool TeamsChatCreatedByWayd { get; init; }

    /// <summary>
    /// True when a Teams connector is present, its configuration is valid, and it is active.
    /// The add control is hidden when this is false. This is not stored on the incident.
    /// </summary>
    public bool TeamsConnectorConfigured { get; init; }

    /// <summary>
    /// How long the noticed, response, and recovery stretches took. A stretch that has not started is null.
    /// </summary>
    public IncidentDurationsDto Durations { get; init; } = new();
}

/// <summary>
/// Durations read from noticed, response started, and recovered. Null is a stretch that has not started.
/// </summary>
public sealed record IncidentDurationsDto
{
    /// <summary>Milliseconds from noticed until response started. Null until response has started.</summary>
    public long? NoticedToResponseStartedMilliseconds { get; init; }

    /// <summary>
    /// Milliseconds from response started until recovered. Null until both are recorded, and null when
    /// the incident is canceled.
    /// </summary>
    public long? ResponseToRecoveredMilliseconds { get; init; }

    /// <summary>
    /// Milliseconds from noticed until recovered, or until now while the incident is still active.
    /// Null when that stretch has no end.
    /// </summary>
    public long? ActiveMilliseconds { get; init; }
}

/// <summary>The active incident that has been open the longest.</summary>
public sealed record LongestActiveIncidentDto
{
    /// <summary>Incident key. The record is addressed by this key.</summary>
    public int Key { get; init; }

    /// <summary>Incident title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Milliseconds from noticed until now.</summary>
    public long ActiveMilliseconds { get; init; }
}

/// <summary>
/// The active count, the longest active incident, and the mean time to recover for every incident
/// matching the state and severity filters.
/// </summary>
public sealed record IncidentFiguresDto
{
    /// <summary>
    /// Incidents that are not Closed or Canceled, and whose status category is not Done or Removed.
    /// </summary>
    public int ActiveCount { get; init; }

    /// <summary>
    /// The active incident open longest, from noticed until now. Null when none are active.
    /// </summary>
    public LongestActiveIncidentDto? LongestActive { get; init; }

    /// <summary>
    /// Mean milliseconds from noticed to recovered for closed incidents. Open and canceled incidents
    /// are excluded. Null when no closed incident has a recovery time.
    /// </summary>
    public long? MeanTimeToRecoverMilliseconds { get; init; }
}

/// <summary>One commander-authored entry on an incident timeline.</summary>
public sealed record IncidentTimelineNoteDto
{
    public Guid Id { get; init; }
    public required string Body { get; init; }
    public Instant RecordedAt { get; init; }
    public EmployeeNavigationDto Author { get; init; } = default!;
}

/// <summary>One link add, update, or remove entry on an incident timeline.</summary>
public sealed record IncidentTimelineLinkEntryDto
{
    public Guid Id { get; init; }
    public IncidentTimelineLinkKind Kind { get; init; }
    public IncidentTimelineLinkAction Action { get; init; }
    public required string Label { get; init; }
    public Instant RecordedAt { get; init; }
    public EmployeeNavigationDto Actor { get; init; } = default!;

    /// <summary>The work item or link this entry is about.</summary>
    public Guid? TargetId { get; init; }

    /// <summary>The work item or link replaced, when the entry records an update.</summary>
    public Guid? FromTargetId { get; init; }
}

internal static class IncidentProjections
{
    internal static StatusNavigationDto Status(Incident incident) =>
        new()
        {
            Id = incident.StatusId,
            Name = incident.StatusName,
            Category = incident.StatusCategory,
            CategoryLabel = WorkflowOwners.CategoryLabel(incident.StatusOwnerType, incident.StatusCategory),
            Alias = incident.StatusAliasValue,
        };

    internal static EmployeeNavigationDto Person(Employee employee) => EmployeeNavigationDto.From(employee);

    /// <summary>
    /// The commander, or null when none is assigned. Throws when one is assigned but the query did not
    /// include it, which would otherwise read as no commander.
    /// </summary>
    private static EmployeeNavigationDto? Commander(Incident incident) =>
        incident.CommanderEmployeeId is null
            ? null
            : Person(Loaded(incident.Commander, nameof(Incident.Commander)));

    /// <summary>
    /// An employee the projection reads, or an exception naming the missing <c>Include</c>. The incident reads
    /// ignore the soft-delete filter, so an assigned employee always loads and a null here is a missing include.
    /// </summary>
    private static Employee Loaded(Employee? employee, string navigation) =>
        employee ?? throw new InvalidOperationException(
            $"The incident query must include {navigation} before projecting it.");

    internal static IncidentSeverityLevelNavigationDto SeverityLevel(IncidentSeverityLevel level) =>
        new()
        {
            Id = level.Id,
            Name = level.Name,
            SortOrder = level.SortOrder,
            Color = level.Color,
        };

    internal static NavigationDto Product(Product product) =>
        NavigationDto.Create(product.Id, product.Key, product.Name);

    internal static NavigationDto Release(Release release) =>
        NavigationDto.Create(release.Id, release.Key, string.IsNullOrWhiteSpace(release.Name) ? release.Version : release.Name);

    internal static IncidentSummaryDto Summary(
        Incident incident,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products,
        IReadOnlyList<Release> releases) =>
        new()
        {
            Id = incident.Id,
            Key = incident.Key,
            Title = incident.Title,
            NoticedAt = incident.NoticedAt,
            StartedAt = incident.StartedAt,
            RecoveredAt = incident.RecoveredAt,
            SubmittedBy = Person(Loaded(incident.SubmittedBy, nameof(Incident.SubmittedBy))),
            ReportedBy = Person(Loaded(incident.ReportedBy, nameof(Incident.ReportedBy))),
            Commander = Commander(incident),
            SeverityLevel = SeverityLevel(level),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
            Products = products.Select(Product).ToList(),
            Releases = releases.Select(Release).ToList(),
        };

    internal static IncidentDto Details(
        Incident incident,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products,
        IReadOnlyList<Release> releases,
        IncidentDurations.Stretches durations,
        bool teamsConnectorConfigured = false) =>
        new()
        {
            Id = incident.Id,
            Key = incident.Key,
            Title = incident.Title,
            Symptoms = incident.Symptoms,
            Remediation = incident.Remediation,
            RootCause = incident.RootCause,
            SubmittedBy = Person(Loaded(incident.SubmittedBy, nameof(Incident.SubmittedBy))),
            ReportedBy = Person(Loaded(incident.ReportedBy, nameof(Incident.ReportedBy))),
            Commander = Commander(incident),
            SeverityLevel = SeverityLevel(level),
            NoticedAt = incident.NoticedAt,
            StartedAt = incident.StartedAt,
            RecoveredAt = incident.RecoveredAt,
            Products = products.Select(Product).ToList(),
            Releases = releases.Select(Release).ToList(),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
            TeamsChatId = incident.TeamsChat?.ExternalId,
            TeamsChatDisplayName = incident.TeamsChat?.Title,
            TeamsChatLink = incident.TeamsChat?.Link,
            TeamsChatCreatedByWayd = incident.TeamsChat?.CreatedByWayd ?? false,
            TeamsConnectorConfigured = teamsConnectorConfigured,
            Durations = new IncidentDurationsDto
            {
                NoticedToResponseStartedMilliseconds = IncidentDurations.Milliseconds(durations.NoticedToResponseStarted),
                ResponseToRecoveredMilliseconds = IncidentDurations.Milliseconds(durations.ResponseToRecovered),
                ActiveMilliseconds = IncidentDurations.Milliseconds(durations.Active),
            },
        };
}
