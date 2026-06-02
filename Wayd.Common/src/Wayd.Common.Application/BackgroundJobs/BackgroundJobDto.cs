namespace Wayd.Common.Application.BackgroundJobs;

public record BackgroundJobDto
{
    public required string Id { get; set; }
    public required string Status { get; set; }
    public required string Namespace { get; set; }
    public required string Type { get; set; }
    public required string Action { get; set; } 
    public bool InProcessingState { get; set; }
    public Instant? StartedAt { get; set; }
}
