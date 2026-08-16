using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>A worker process polling for jobs. A stale heartbeat indicates a wedged or stopped worker.</summary>
public class JobServerResponse
{
    public string Name { get; set; } = default!;
    public int WorkerCount { get; set; }
    public List<string> Queues { get; set; } = [];
    public Instant? StartedAt { get; set; }
    public Instant? Heartbeat { get; set; }

    internal static JobServerResponse From(JobServerDto server) => new()
    {
        Name = server.Name,
        WorkerCount = server.WorkerCount,
        Queues = [.. server.Queues],
        StartedAt = server.StartedAt,
        Heartbeat = server.Heartbeat,
    };
}
