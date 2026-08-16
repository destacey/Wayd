using Wayd.Common.Application.BackgroundJobs;

namespace Wayd.Web.Api.Models.Admin.BackgroundJobs;

/// <summary>A registered recurring job.</summary>
public class RecurringJobResponse
{
    public string Id { get; set; } = default!;
    public string? Cron { get; set; }
    public string? Queue { get; set; }

    /// <summary>Null when the stored invocation data no longer resolves to a loadable method; <see cref="Error"/> carries the reason.</summary>
    public string? Type { get; set; }

    public string? Action { get; set; }
    public Instant? LastExecution { get; set; }
    public Instant? NextExecution { get; set; }
    public string? LastJobId { get; set; }
    public string? LastJobState { get; set; }
    public string? Error { get; set; }

    internal static RecurringJobResponse From(RecurringJobDto job) => new()
    {
        Id = job.Id,
        Cron = job.Cron,
        Queue = job.Queue,
        Type = job.Type,
        Action = job.Action,
        LastExecution = job.LastExecution,
        NextExecution = job.NextExecution,
        LastJobId = job.LastJobId,
        LastJobState = job.LastJobState,
        Error = job.Error,
    };
}
