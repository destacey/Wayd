using Wayd.Common.Application.Dtos;
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
    public NavigationDto SubmittedBy { get; init; } = default!;

    /// <summary>The employee who reported the incident.</summary>
    public NavigationDto ReportedBy { get; init; } = default!;

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public NavigationDto? Commander { get; init; }
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
    public NavigationDto SubmittedBy { get; init; } = default!;

    /// <summary>The employee who reported the incident.</summary>
    public NavigationDto ReportedBy { get; init; } = default!;

    /// <summary>The employee commanding the response, when one is assigned.</summary>
    public NavigationDto? Commander { get; init; }
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
    /// True when a Teams connector is configured. The chat control is hidden when this is false.
    /// This is not stored on the incident.
    /// </summary>
    public bool TeamsConnectorConfigured { get; init; }
}

/// <summary>One commander-authored entry on an incident timeline.</summary>
public sealed record IncidentTimelineNoteDto
{
    public Guid Id { get; init; }
    public required string Body { get; init; }
    public Instant RecordedAt { get; init; }
    public NavigationDto Author { get; init; } = default!;
}

/// <summary>One link add, update, or remove entry on an incident timeline.</summary>
public sealed record IncidentTimelineLinkEntryDto
{
    public Guid Id { get; init; }
    public IncidentTimelineLinkKind Kind { get; init; }
    public IncidentTimelineLinkAction Action { get; init; }
    public required string Label { get; init; }
    public Instant RecordedAt { get; init; }
    public NavigationDto Actor { get; init; } = default!;

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

    internal static NavigationDto Person(Employee employee) =>
        NavigationDto.Create(employee.Id, employee.Key, employee.Name.FullName);

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
        Employee submittedBy,
        Employee reportedBy,
        Employee? commander,
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
            SubmittedBy = Person(submittedBy),
            ReportedBy = Person(reportedBy),
            Commander = commander is null ? null : Person(commander),
            SeverityLevel = SeverityLevel(level),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
            Products = products.Select(Product).ToList(),
            Releases = releases.Select(Release).ToList(),
        };

    internal static IncidentDto Details(
        Incident incident,
        Employee submittedBy,
        Employee reportedBy,
        Employee? commander,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products,
        IReadOnlyList<Release> releases,
        bool teamsConnectorConfigured = false) =>
        new()
        {
            Id = incident.Id,
            Key = incident.Key,
            Title = incident.Title,
            Symptoms = incident.Symptoms,
            Remediation = incident.Remediation,
            RootCause = incident.RootCause,
            SubmittedBy = Person(submittedBy),
            ReportedBy = Person(reportedBy),
            Commander = commander is null ? null : Person(commander),
            SeverityLevel = SeverityLevel(level),
            NoticedAt = incident.NoticedAt,
            StartedAt = incident.StartedAt,
            RecoveredAt = incident.RecoveredAt,
            Products = products.Select(Product).ToList(),
            Releases = releases.Select(Release).ToList(),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
            TeamsChatId = incident.TeamsChatId,
            TeamsChatDisplayName = incident.TeamsChatDisplayName,
            TeamsChatLink = incident.TeamsChatLink,
            TeamsChatCreatedByWayd = incident.TeamsChatCreatedByWayd,
            TeamsConnectorConfigured = teamsConnectorConfigured,
        };
}
