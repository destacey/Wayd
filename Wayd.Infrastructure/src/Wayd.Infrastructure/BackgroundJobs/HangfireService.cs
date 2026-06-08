using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using NodaTime;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Application.Identity;

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