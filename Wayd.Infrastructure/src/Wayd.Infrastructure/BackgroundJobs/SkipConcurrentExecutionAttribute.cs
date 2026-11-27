using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;

namespace Wayd.Infrastructure.BackgroundJobs;

/// <summary>
/// Lets one run of a job method go at a time, and skips a run that finds another already doing its work: the skipped
/// run ends in Deleted with a reason that says so, at once, without failing or retrying.
/// </summary>
/// <remarks>
/// <para>
/// For a job whose overlapping run would repeat work the running one already does, such as a sync or a sweep. Without
/// an <see cref="IRunCoverage"/>, every run of the method is taken to do the same work, so an overlapping run is always
/// skipped. With one, an overlapping run that no run in progress covers waits for the lock instead, as under
/// <see cref="Hangfire.DisableConcurrentExecutionAttribute"/>, and fails when the wait times out: its work did not happen.
/// </para>
/// <para>
/// A job whose every queued run carries work of its own, such as one per record, keeps
/// <see cref="Hangfire.DisableConcurrentExecutionAttribute"/> and waits its turn instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipConcurrentExecutionAttribute : JobFilterAttribute, IServerFilter, IElectStateFilter
{
    private static readonly ILog _logger = LogProvider.GetCurrentClassLogger();

    private const string LockItemKey = "SkipConcurrentExecution.Lock";

    /// <summary>
    /// The job parameter a run of a job with an <see cref="IRunCoverage"/> sets once it holds the lock, so a run
    /// locked out can tell the holder from runs still waiting. Only the holder is doing its work: a waiter may be
    /// overtaken or time out, so a run skipped on a waiter's account could lose its work.
    /// </summary>
    private const string HoldsLockParameter = "SkipConcurrentExecution.HoldsLock";
    private const string Holding = "true";
    private const string NotHolding = "false";

    /// <summary>How many processing jobs are read to find the runs in progress; far above any server's worker count.</summary>
    private const int ProcessingJobsRead = 1000;

    /// <summary>
    /// The reason Hangfire's worker gives the Deleted state of a run this filter canceled. The worker names the
    /// canceling filter by its type name; the election below replaces the reason with one a reader can act on.
    /// </summary>
    internal const string CanceledReason = $"Canceled by filter '{nameof(SkipConcurrentExecutionAttribute)}'";

    private readonly IRunCoverage? _coverage;
    private readonly TimeSpan _waitTimeout;

    /// <summary>Skips every run that starts while another run of the method is in progress.</summary>
    public SkipConcurrentExecutionAttribute()
    {
    }

    /// <summary>
    /// Skips a run that starts while a run of the method doing all its work is in progress; any other overlapping run
    /// waits up to <paramref name="waitTimeoutInSeconds"/> for the lock, then fails.
    /// </summary>
    /// <param name="coverageType">An <see cref="IRunCoverage"/> with a parameterless constructor.</param>
    /// <param name="waitTimeoutInSeconds">How long a run that is not covered waits for the lock.</param>
    public SkipConcurrentExecutionAttribute(Type coverageType, int waitTimeoutInSeconds)
    {
        ArgumentNullException.ThrowIfNull(coverageType);
        ArgumentOutOfRangeException.ThrowIfNegative(waitTimeoutInSeconds);
        if (!typeof(IRunCoverage).IsAssignableFrom(coverageType))
            throw new ArgumentException($"{coverageType.Name} does not implement {nameof(IRunCoverage)}.", nameof(coverageType));

        _coverage = (IRunCoverage)Activator.CreateInstance(coverageType)!;
        _waitTimeout = TimeSpan.FromSeconds(waitTimeoutInSeconds);
    }

    /// <summary>The run's <see cref="IRunCoverage"/> type, or null when every run of the method does the same work.</summary>
    public Type? CoverageType => _coverage?.GetType();

    /// <summary>How long a run no run in progress covers waits for the lock; zero without an <see cref="IRunCoverage"/>.</summary>
    public TimeSpan WaitTimeout => _waitTimeout;

    /// <summary>The reason a skipped run's Deleted state carries, naming the job method that was already running.</summary>
    public static string SkippedReason(string jobMethodName) =>
        $"Skipped: another run of {jobMethodName} was already in progress.";

    /// <summary>
    /// Takes the job method's lock without waiting. When another run holds it, cancels this run if a run in progress
    /// covers it, and otherwise waits for the lock.
    /// </summary>
    public void OnPerforming(PerformingContext context)
    {
        var job = context.BackgroundJob.Job;
        var resource = LockResource(job);

        // A retried or re-fetched run keeps its parameters, so an earlier attempt's mark is cleared before this one waits.
        if (_coverage is not null
            && context.BackgroundJob.ParametersSnapshot?.TryGetValue(HoldsLockParameter, out var mark) == true
            && mark == Holding)
        {
            context.Connection.SetJobParameter(context.BackgroundJob.Id, HoldsLockParameter, NotHolding);
        }

        try
        {
            TookLock(context, context.Connection.AcquireDistributedLock(resource, TimeSpan.Zero));
            return;
        }
        catch (DistributedLockTimeoutException)
        {
            if (IsCovered(context))
            {
                _logger.InfoFormat(
                    "Job Service: Skipping job {0}: another run of {1} is already in progress",
                    context.BackgroundJob.Id,
                    job.Method.Name);

                // A canceled run is moved to Deleted by the worker, never to Failed, so no retry follows.
                context.Canceled = true;
                return;
            }
        }

        // Not covered: wait as DisableConcurrentExecution does. A timeout propagates and fails the run.
        TookLock(context, context.Connection.AcquireDistributedLock(resource, _waitTimeout));
    }

    private void TookLock(PerformingContext context, IDisposable distributedLock)
    {
        context.Items[LockItemKey] = distributedLock;

        // The mark is left on the job when the run ends: a finished run is no longer processing, so it is never read.
        if (_coverage is not null)
            context.Connection.SetJobParameter(context.BackgroundJob.Id, HoldsLockParameter, Holding);
    }

    /// <summary>Releases the lock the run took.</summary>
    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue(LockItemKey, out var distributedLock))
        {
            ((IDisposable)distributedLock).Dispose();
        }
    }

    /// <summary>Gives a skipped run's Deleted state the reason it was skipped.</summary>
    /// <remarks>
    /// Rewrites the candidate's reason rather than electing a new state: Hangfire records a replaced candidate in the
    /// job's history, which would show the run deleted twice.
    /// </remarks>
    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is DeletedState { Reason: CanceledReason } deleted)
        {
            deleted.Reason = SkippedReason(context.BackgroundJob.Job.Method.Name);
        }
    }

    /// <summary>
    /// Whether the run holding the method's lock does all this run's work. Only the holder counts, never another run
    /// still waiting (see <see cref="HoldsLockParameter"/>). A holder that has not yet marked itself is not found, so
    /// the run waits: the safe outcome.
    /// </summary>
    private bool IsCovered(PerformingContext context)
    {
        if (_coverage is null)
            return true;

        var started = context.BackgroundJob.Job;
        return context.Storage.GetMonitoringApi()
            .ProcessingJobs(0, ProcessingJobsRead)
            .Where(j => j.Key != context.BackgroundJob.Id && j.Value.Job is not null)
            .Where(j => LockResource(j.Value.Job) == LockResource(started))
            .Where(j => context.Connection.GetJobParameter(j.Key, HoldsLockParameter) == Holding)
            .Any(j => _coverage.Covers(j.Value.Job, started));
    }

    /// <summary>
    /// The lock's name: the name <see cref="Hangfire.DisableConcurrentExecutionAttribute"/> takes for a top-level,
    /// non-generic job type such as <c>JobManager</c>, so a run under either attribute excludes a run under the other.
    /// </summary>
    internal static string LockResource(Job job) => $"{job.Type.Name}.{job.Method.Name}";
}
