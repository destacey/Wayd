using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Dtos;

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
    public NavigationDto SeverityLevel { get; init; } = default!;
    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }
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
    public NavigationDto SeverityLevel { get; init; } = default!;
    public Instant NoticedAt { get; init; }
    public Instant? StartedAt { get; init; }
    public Instant? RecoveredAt { get; init; }
    public IReadOnlyList<NavigationDto> Products { get; init; } = [];
    public StatusNavigationDto Status { get; init; } = default!;
    public ProductStatusAlias StatusAlias { get; init; }
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

    internal static NavigationDto SeverityLevel(IncidentSeverityLevel level) =>
        NavigationDto.Create(level.Id, level.SortOrder, level.Name);

    internal static NavigationDto Product(Product product) =>
        NavigationDto.Create(product.Id, product.Key, product.Name);

    internal static IncidentSummaryDto Summary(Incident incident, Employee commander, IncidentSeverityLevel level) =>
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
        };

    internal static IncidentDto Details(
        Incident incident,
        Employee commander,
        IncidentSeverityLevel level,
        IReadOnlyList<Product> products) =>
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
            Status = Status(incident),
            StatusAlias = incident.StatusAlias,
        };
}
