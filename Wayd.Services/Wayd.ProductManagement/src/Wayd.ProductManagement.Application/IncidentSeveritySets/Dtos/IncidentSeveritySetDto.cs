namespace Wayd.ProductManagement.Application.IncidentSeveritySets.Dtos;

public sealed class IncidentSeverityLevelDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
}

public sealed class IncidentSeveritySetDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public bool IsSelected { get; set; }
    public List<IncidentSeverityLevelDto> Levels { get; set; } = [];
}
