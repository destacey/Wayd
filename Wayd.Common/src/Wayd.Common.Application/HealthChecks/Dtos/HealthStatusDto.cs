namespace Wayd.Common.Application.HealthChecks.Dtos;

public sealed record HealthStatusDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
}
