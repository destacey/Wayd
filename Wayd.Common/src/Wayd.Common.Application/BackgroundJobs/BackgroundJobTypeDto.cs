namespace Wayd.Common.Application.BackgroundJobs;

public sealed record BackgroundJobTypeDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int Order { get; set; }
    public required string GroupName { get; set; }

    /// <summary>
    /// Whether this type can be registered as a recurring job. Every type can be run on demand
    /// regardless of this value.
    /// </summary>
    public bool IsSchedulable { get; set; }
}
