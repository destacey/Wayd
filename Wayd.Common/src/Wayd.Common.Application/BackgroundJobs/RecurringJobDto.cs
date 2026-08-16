namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// A registered recurring job. Until this was surfaced, recurring jobs could be created through the
/// Wayd UI but only listed or removed from the scheduler's own dashboard.
/// </summary>
public sealed record RecurringJobDto
{
    public required string Id { get; set; }
    public string? Cron { get; set; }
    public string? Queue { get; set; }

    /// <summary>Declaring type of the scheduled method, without namespace. Null if the job's invocation data no longer resolves.</summary>
    public string? Type { get; set; }

    public string? Action { get; set; }

    public Instant? LastExecution { get; set; }
    public Instant? NextExecution { get; set; }

    /// <summary>Id of the job the last trigger created, for linking through to its outcome.</summary>
    public string? LastJobId { get; set; }

    public string? LastJobState { get; set; }

    /// <summary>Set when the stored cron expression or invocation data could not be parsed.</summary>
    public string? Error { get; set; }
}
