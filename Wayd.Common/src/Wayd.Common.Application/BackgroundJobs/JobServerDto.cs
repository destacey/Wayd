namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// A worker process polling for jobs. A stale <see cref="Heartbeat"/> is how a wedged or
/// scaled-to-zero worker shows up.
/// </summary>
public sealed record JobServerDto
{
    public required string Name { get; set; }
    public int WorkerCount { get; set; }
    public IReadOnlyList<string> Queues { get; set; } = [];
    public Instant? StartedAt { get; set; }
    public Instant? Heartbeat { get; set; }
}
