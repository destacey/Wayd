using System.Linq.Expressions;

namespace Wayd.Common.Application.BackgroundJobs;

public interface IJobService : IScopedService
{
    IEnumerable<BackgroundJobDto> GetRunningJobs();

    /// <summary>A page of jobs in one lifecycle state. <paramref name="pageNumber"/> is 0-based.</summary>
    JobPageDto GetJobs(JobStateFilter state, int pageNumber, int pageSize);

    /// <summary>Full detail for one job, or null when the id is unknown or its record has expired.</summary>
    JobDetailDto? GetJobDetail(string jobId);

    JobStatisticsDto GetStatistics();

    IReadOnlyList<JobServerDto> GetServers();

    IReadOnlyList<RecurringJobDto> GetRecurringJobs();

    /// <summary>Removes a recurring registration. Returns false when no job with that id exists.</summary>
    bool RemoveRecurringJob(string recurringJobId);

    string Enqueue(Expression<Action> methodCall);

    string Enqueue(Expression<Func<Task>> methodCall);

    string EnqueueSystem(Expression<Func<Task>> methodCall);

    string Enqueue<T>(Expression<Action<T>> methodCall);

    string Enqueue<T>(Expression<Func<T, Task>> methodCall);

    string Schedule(Expression<Action> methodCall, TimeSpan delay);

    string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay);

    string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt);

    string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt);

    string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay);

    string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);

    string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt);

    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt);

    bool Delete(string jobId);

    bool Delete(string jobId, string fromState);

    bool Requeue(string jobId);

    bool Requeue(string jobId, string fromState);

    void AddOrUpdate(string jobId, Expression<Func<Task>> methodCall, Func<string> cronExpression);
}