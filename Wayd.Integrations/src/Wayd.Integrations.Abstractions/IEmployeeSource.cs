namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Connector-neutral contract for pulling employees from an external people system. One
/// implementation per connector, registered as a keyed transient by <see cref="Connector"/>
/// value. The runner (<c>IPeopleSyncRunner</c>) consumes this interface and is unaware of any
/// connector-specific concepts such as Graph credentials or Workday SOAP contexts.
/// </summary>
public interface IEmployeeSource
{
    /// <summary>The connector this source serves.</summary>
    Connector Connector { get; }

    /// <summary>
    /// True when this source can return a delta of changed employees since a given timestamp.
    /// The runner uses it to (a) tag the SyncRun as Differential and (b) skip the deactivation
    /// pass on incremental runs.
    /// </summary>
    bool SupportsIncremental { get; }

    /// <summary>
    /// The employee property the bulk upsert matches existing rows on. Read from the bound
    /// connection's configuration — valid only after <see cref="Bind"/>.
    /// </summary>
    EmployeeMatchProperty MatchBy { get; }

    /// <summary>
    /// Bind this source instance to a specific connection. Called once by the factory before
    /// <see cref="GetEmployees"/> is invoked. Sources should cast the descriptor's boxed
    /// configuration here and fail fast if the shape is wrong.
    /// </summary>
    Result Bind(SyncableConnectionDescriptor descriptor);

    /// <summary>
    /// Fetch employees from the external system. <paramref name="since"/> is the incremental
    /// watermark — null means full snapshot. The runner only passes a watermark when
    /// <see cref="SupportsIncremental"/> is true and a prior successful run exists.
    /// </summary>
    Task<Result<EmployeeFetchResult>> GetEmployees(Instant? since, CancellationToken cancellationToken);
}
