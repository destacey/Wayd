namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>
/// A job as it appears in a list. The timestamp that matters differs by state (started, scheduled
/// for, failed at, succeeded at), so a single <see cref="Timestamp"/> carries whichever one the
/// requested bucket defines, and <see cref="TimestampLabel"/> names it for the UI column header.
/// </summary>
public sealed record JobSummaryDto
{
    public required string Id { get; set; }
    public required string State { get; set; }

    /// <summary>Declaring type of the job method, without namespace.</summary>
    public required string Type { get; set; }

    public required string Namespace { get; set; }

    /// <summary>The job method name.</summary>
    public required string Action { get; set; }

    public Instant? Timestamp { get; set; }
    public string? TimestampLabel { get; set; }

    /// <summary>Populated for failed jobs only; the exception type that ended the attempt.</summary>
    public string? ExceptionType { get; set; }

    public string? ExceptionMessage { get; set; }
}
