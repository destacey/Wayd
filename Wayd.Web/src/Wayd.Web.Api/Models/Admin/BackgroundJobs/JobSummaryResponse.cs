using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>A job as it appears in a list.</summary>
public class JobSummaryResponse
{
    public string Id { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Namespace { get; set; } = default!;
    public string Action { get; set; } = default!;

    /// <summary>The timestamp that matters for the requested state; <see cref="TimestampLabel"/> names it.</summary>
    public Instant? Timestamp { get; set; }

    public string? TimestampLabel { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }

    internal static JobSummaryResponse From(JobSummaryDto job) => new()
    {
        Id = job.Id,
        State = job.State,
        Type = job.Type,
        Namespace = job.Namespace,
        Action = job.Action,
        Timestamp = job.Timestamp,
        TimestampLabel = job.TimestampLabel,
        ExceptionType = job.ExceptionType,
        ExceptionMessage = job.ExceptionMessage,
    };
}
