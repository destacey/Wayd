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
    public NavigationDto Commander { get; init; } = default!;
    public IncidentSeverityLevelNavigationDto SeverityLevel { get; init; } = default!;
    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }
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
    public string? Description { get; init; }
    public NavigationDto Commander { get; init; } = default!;
    public IncidentSeverityLevelNavigationDto SeverityLevel { get; init; } = default!;
    public Instant NoticedAt { get; init; }
    public Instant? StartedAt { get; init; }
    public Instant? RecoveredAt { get; init; }
    public IReadOnlyList<NavigationDto> Products { get; init; } = [];

    /// <summary>Releases this incident points at. One incident may point at several.</summary>
    public IReadOnlyList<NavigationDto> Releases { get; init; } = [];

    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }
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

    internal static NavigationDto Commander(Employee employee) =>
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
        Employee commander,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products) =>
        new()
        {
            Id = incident.Id,
            Key = incident.Key,
            Title = incident.Title,
            NoticedAt = incident.NoticedAt,
            StartedAt = incident.StartedAt,
            RecoveredAt = incident.RecoveredAt,
            Commander = Commander(commander),
            SeverityLevel = SeverityLevel(level),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
            Products = products.Select(Product).ToList(),
        };

    internal static IncidentDto Details(
        Incident incident,
        Employee commander,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products,
        IReadOnlyList<Release> releases) =>
        new()
        {
            Id = incident.Id,
            Key = incident.Key,
            Title = incident.Title,
            Description = incident.Description,
            Commander = Commander(commander),
            SeverityLevel = SeverityLevel(level),
            NoticedAt = incident.NoticedAt,
            StartedAt = incident.StartedAt,
            RecoveredAt = incident.RecoveredAt,
            Products = products.Select(Product).ToList(),
            Releases = releases.Select(Release).ToList(),
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
        };
}
