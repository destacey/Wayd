using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>
/// Full detail for one job, including the failure stack trace that was previously only visible in
/// the Hangfire dashboard.
/// </summary>
public class JobDetailResponse
{
    public string Id { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Namespace { get; set; } = default!;
    public string Action { get; set; } = default!;
    public Instant? CreatedAt { get; set; }
    public Instant? ExpiresAt { get; set; }
    public List<string> Arguments { get; set; } = [];
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionDetails { get; set; }

    /// <summary>State transitions, most recent first.</summary>
    public List<JobStateHistoryResponse> History { get; set; } = [];

    internal static JobDetailResponse From(JobDetailDto job) => new()
    {
        Id = job.Id,
        State = job.State,
        Type = job.Type,
        Namespace = job.Namespace,
        Action = job.Action,
        CreatedAt = job.CreatedAt,
        ExpiresAt = job.ExpiresAt,
        Arguments = [.. job.Arguments],
        ExceptionType = job.ExceptionType,
        ExceptionMessage = job.ExceptionMessage,
        ExceptionDetails = job.ExceptionDetails,
        History = [.. job.History.Select(JobStateHistoryResponse.From)],
    };
}

public class JobStateHistoryResponse
{
    public string State { get; set; } = default!;
    public string? Reason { get; set; }
    public Instant? ChangedAt { get; set; }

    internal static JobStateHistoryResponse From(JobStateHistoryDto history) => new()
    {
        State = history.State,
        Reason = history.Reason,
        ChangedAt = history.ChangedAt,
    };
}
