namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// Everything known about a single job: what it invokes, how it got to its current state, and — when
/// it failed — the exception detail that would otherwise only be visible in the scheduler's own UI.
/// </summary>
public sealed record JobDetailDto
{
    public required string Id { get; set; }
    public required string State { get; set; }
    public required string Type { get; set; }
    public required string Namespace { get; set; }
    public required string Action { get; set; }

    public Instant? CreatedAt { get; set; }

    /// <summary>When the job's storage record is purged. Null for jobs still in flight.</summary>
    public Instant? ExpiresAt { get; set; }

    /// <summary>Arguments the job method was invoked with, in parameter order.</summary>
    public IReadOnlyList<string> Arguments { get; set; } = [];

    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }

    /// <summary>Full stack trace of the failure, when the job is in a failed state.</summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>State transitions, most recent first.</summary>
    public IReadOnlyList<JobStateHistoryDto> History { get; set; } = [];
}

public sealed record JobStateHistoryDto
{
    public required string State { get; set; }
    public string? Reason { get; set; }
    public Instant? ChangedAt { get; set; }
}
