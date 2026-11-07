namespace Wayd.Common.Application.BackgroundJobs;

/// <summary>What happened when a job was asked to run again.</summary>
public enum RequeueOutcome
{
    /// <summary>The job was moved back to the queue.</summary>
    Requeued,

    /// <summary>No job has that id, or its record has expired.</summary>
    NotFound,

    /// <summary>
    /// A worker is running the job. Requeueing would start a second copy while the first keeps going,
    /// so the job is left alone.
    /// </summary>
    Running,

    /// <summary>The job left the state it was read in before it could be requeued, so it was left alone.</summary>
    StateChanged,
}
