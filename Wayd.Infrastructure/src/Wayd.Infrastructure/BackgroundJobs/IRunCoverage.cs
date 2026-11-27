using Hangfire.Common;

namespace Wayd.Infrastructure.BackgroundJobs;

/// <summary>
/// Decides, for a job method whose runs differ by their arguments, whether a run in progress already does all the
/// work of a newly started run of the same method, so the new run can be skipped.
/// </summary>
/// <remarks>Instantiated once per attribute and shared across runs, so an implementation must be stateless.</remarks>
public interface IRunCoverage
{
    /// <summary>Whether <paramref name="running"/> does all the work <paramref name="started"/> would do.</summary>
    bool Covers(Job running, Job started);
}
