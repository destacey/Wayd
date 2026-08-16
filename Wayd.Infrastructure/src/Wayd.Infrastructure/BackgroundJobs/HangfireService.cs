using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NodaTime;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Application.Identity;

// Hangfire.Storage has its own RecurringJobDto; alias Wayd's so the mapping below names both sides.
using WaydRecurringJobDto = Wayd.Common.Application.BackgroundJobs.RecurringJobDto;

namespace Wayd.Infrastructure.BackgroundJobs;

public class HangfireService : IJobService
{
    public IEnumerable<BackgroundJobDto> GetRunningJobs()
    {
        List<BackgroundJobDto> backgroundJobs = [];

        var jobs = JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, 1000);

        foreach (var job in jobs)
        {
            backgroundJobs.Add(new BackgroundJobDto
            {
                Id = job.Key,
                Status = job.Value.InProcessingState ? "Running" : "Not Running",
                Type = job.Value.Job.Type.Name,
                Namespace = job.Value.Job.Type.Namespace ?? "Unknown",
                Action = job.Value.Job.Method.Name,
                InProcessingState = job.Value.InProcessingState,
                StartedAt = job.Value.StartedAt is not null ? Instant.FromDateTimeUtc(DateTime.SpecifyKind((DateTime)job.Value.StartedAt, DateTimeKind.Utc)) : null
            });
        }
        return backgroundJobs;
    }

    public JobPageDto GetJobs(JobStateFilter state, int pageNumber, int pageSize)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();
        var from = pageNumber * pageSize;

        return state switch
        {
            JobStateFilter.Processing => Page(
                monitoring.ProcessingCount(),
                monitoring.ProcessingJobs(from, pageSize),
                (id, job) => Summary(id, job.Job, "Processing", ToInstant(job.StartedAt), "Started")),

            JobStateFilter.Scheduled => Page(
                monitoring.ScheduledCount(),
                monitoring.ScheduledJobs(from, pageSize),
                (id, job) => Summary(id, job.Job, "Scheduled", ToInstant(job.EnqueueAt), "Runs At")),

            JobStateFilter.Failed => Page(
                monitoring.FailedCount(),
                monitoring.FailedJobs(from, pageSize),
                (id, job) => Summary(id, job.Job, "Failed", ToInstant(job.FailedAt), "Failed") with
                {
                    ExceptionType = job.ExceptionType,
                    ExceptionMessage = job.ExceptionMessage,
                }),

            JobStateFilter.Succeeded => Page(
                monitoring.SucceededListCount(),
                monitoring.SucceededJobs(from, pageSize),
                (id, job) => Summary(id, job.Job, "Succeeded", ToInstant(job.SucceededAt), "Succeeded")),

            JobStateFilter.Deleted => Page(
                monitoring.DeletedListCount(),
                monitoring.DeletedJobs(from, pageSize),
                (id, job) => Summary(id, job.Job, "Deleted", ToInstant(job.DeletedAt), "Deleted")),

            // Enqueued is per-queue in Hangfire, with no all-queues overload. Walking the queues and
            // concatenating keeps one API shape for the UI; the page window is applied across the
            // flattened result rather than per queue.
            JobStateFilter.Enqueued => EnqueuedPage(monitoring, from, pageSize),

            _ => new JobPageDto { PageNumber = pageNumber, PageSize = pageSize },
        };

        JobPageDto Page<T>(long total, JobList<T> jobs, Func<string, T, JobSummaryDto> map) => new()
        {
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = [.. jobs.Select(j => map(j.Key, j.Value))],
        };
    }

    private static JobPageDto EnqueuedPage(IMonitoringApi monitoring, int from, int pageSize)
    {
        var queues = monitoring.Queues();
        var total = queues.Sum(q => q.Length);

        var items = queues
            .SelectMany(queue => monitoring
                .EnqueuedJobs(queue.Name, 0, from + pageSize)
                .Select(j => Summary(j.Key, j.Value.Job, "Enqueued", ToInstant(j.Value.EnqueuedAt), "Enqueued")))
            .Skip(from)
            .Take(pageSize)
            .ToList();

        return new JobPageDto
        {
            TotalCount = total,
            PageNumber = from / Math.Max(pageSize, 1),
            PageSize = pageSize,
            Items = items,
        };
    }

    public JobDetailDto? GetJobDetail(string jobId)
    {
        var monitoring = JobStorage.Current.GetMonitoringApi();

        var details = monitoring.JobDetails(jobId);
        if (details is null)
        {
            return null;
        }

        // Hangfire records history newest-last; the UI reads it as a timeline, most recent first.
        var history = details.History?.Reverse().Select(h => new JobStateHistoryDto
        {
            State = h.StateName,
            Reason = h.Reason,
            ChangedAt = ToInstant(h.CreatedAt),
        }).ToList() ?? [];

        // Exception detail lives on the failed state's data bag, not on JobDetailsDto.
        var failure = details.History?.FirstOrDefault(h => h.StateName == FailedStateName);

        return new JobDetailDto
        {
            Id = jobId,
            State = history.FirstOrDefault()?.State ?? "Unknown",
            Type = details.Job?.Type.Name ?? UnknownValue,
            Namespace = details.Job?.Type.Namespace ?? UnknownValue,
            Action = details.Job?.Method.Name ?? UnknownValue,
            CreatedAt = ToInstant(details.CreatedAt),
            ExpiresAt = ToInstant(details.ExpireAt),
            Arguments = [.. details.Job?.Args?.Select(a => a?.ToString() ?? "null") ?? []],
            ExceptionType = ReadStateData(failure, "ExceptionType"),
            ExceptionMessage = ReadStateData(failure, "ExceptionMessage"),
            ExceptionDetails = ReadStateData(failure, "ExceptionDetails"),
            History = history,
        };
    }

    public JobStatisticsDto GetStatistics()
    {
        var statistics = JobStorage.Current.GetMonitoringApi().GetStatistics();

        return new JobStatisticsDto
        {
            Enqueued = statistics.Enqueued,
            Scheduled = statistics.Scheduled,
            Processing = statistics.Processing,
            Succeeded = statistics.Succeeded,
            Failed = statistics.Failed,
            Deleted = statistics.Deleted,
            Recurring = statistics.Recurring,
            Servers = statistics.Servers,
        };
    }

    public IReadOnlyList<JobServerDto> GetServers() =>
    [
        .. JobStorage.Current.GetMonitoringApi().Servers().Select(server => new JobServerDto
        {
            Name = server.Name,
            WorkerCount = server.WorkersCount,
            Queues = [.. server.Queues ?? []],
            StartedAt = ToInstant(server.StartedAt),
            Heartbeat = ToInstant(server.Heartbeat),
        })
    ];

    public IReadOnlyList<WaydRecurringJobDto> GetRecurringJobs()
    {
        using var connection = JobStorage.Current.GetConnection();

        return
        [
            .. connection.GetRecurringJobs().Select(recurring => new WaydRecurringJobDto
            {
                Id = recurring.Id,
                Cron = recurring.Cron,
                Queue = recurring.Queue,
                // Job is null when the stored invocation data no longer resolves to a loadable
                // method (renamed or removed); Error carries the reason.
                Type = recurring.Job?.Type.Name,
                Action = recurring.Job?.Method.Name,
                LastExecution = ToInstant(recurring.LastExecution),
                NextExecution = ToInstant(recurring.NextExecution),
                LastJobId = recurring.LastJobId,
                LastJobState = recurring.LastJobState,
                Error = recurring.Error,
            })
        ];
    }

    public bool RemoveRecurringJob(string recurringJobId)
    {
        using var connection = JobStorage.Current.GetConnection();

        // RemoveIfExists returns void, so existence is checked first to tell "removed" from
        // "no such job" — the API turns the latter into a 404 rather than a silent success.
        var exists = connection.GetRecurringJobs().Any(j => j.Id == recurringJobId);
        if (!exists)
        {
            return false;
        }

        RecurringJob.RemoveIfExists(recurringJobId);
        return true;
    }

    private const string FailedStateName = "Failed";
    private const string UnknownValue = "Unknown";

    private static JobSummaryDto Summary(string id, Job? job, string state, Instant? timestamp, string timestampLabel) => new()
    {
        Id = id,
        State = state,
        Type = job?.Type.Name ?? UnknownValue,
        Namespace = job?.Type.Namespace ?? UnknownValue,
        Action = job?.Method.Name ?? UnknownValue,
        Timestamp = timestamp,
        TimestampLabel = timestampLabel,
    };

    private static string? ReadStateData(StateHistoryDto? state, string key) =>
        state?.Data is not null && state.Data.TryGetValue(key, out var value) ? value : null;

    private static Instant? ToInstant(DateTime? value) =>
        value is null ? null : Instant.FromDateTimeUtc(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    public bool Delete(string jobId) =>
        BackgroundJob.Delete(jobId);

    public bool Delete(string jobId, string fromState) =>
        BackgroundJob.Delete(jobId, fromState);

    public string Enqueue(Expression<Func<Task>> methodCall) =>
        BackgroundJob.Enqueue(methodCall);

    public string EnqueueSystem(Expression<Func<Task>> methodCall)
    {
        var jobId = BackgroundJob.Enqueue(methodCall);
        using var connection = JobStorage.Current.GetConnection();
        connection.SetJobParameter(jobId, QueryStringKeys.UserId, SerializationHelper.Serialize(SystemIdentity.UserId));
        return jobId;
    }

    public string Enqueue<T>(Expression<Action<T>> methodCall) =>
        BackgroundJob.Enqueue(methodCall);

    public string Enqueue(Expression<Action> methodCall) =>
        BackgroundJob.Enqueue(methodCall);

    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) =>
        BackgroundJob.Enqueue(methodCall);

    public bool Requeue(string jobId) =>
        BackgroundJob.Requeue(jobId);

    public bool Requeue(string jobId, string fromState) =>
        BackgroundJob.Requeue(jobId, fromState);

    public string Schedule(Expression<Action> methodCall, TimeSpan delay) =>
        BackgroundJob.Schedule(methodCall, delay);

    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay) =>
        BackgroundJob.Schedule(methodCall, delay);

    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt) =>
        BackgroundJob.Schedule(methodCall, enqueueAt);

    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt) =>
        BackgroundJob.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) =>
        BackgroundJob.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) =>
        BackgroundJob.Schedule(methodCall, delay);

    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) =>
        BackgroundJob.Schedule(methodCall, enqueueAt);

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt) =>
        BackgroundJob.Schedule(methodCall, enqueueAt);

    // public static void AddOrUpdate([NotNull] string recurringJobId, [NotNull][InstantHandle] Expression<Action> methodCall, [NotNull] Func<string> cronExpression, [NotNull] RecurringJobOptions options)
    public void AddOrUpdate(string jobId, Expression<Func<Task>> methodCall, Func<string> cronExpression) =>
        RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, new RecurringJobOptions());
}