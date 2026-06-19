using Wayd.Common.Application.Enums;

namespace Wayd.AppIntegration.Application.Interfaces;

public interface IPeopleSyncRunner : ITransientService
{
    /// <summary>
    /// Runs people sync across all active PeopleSync-category connections.
    /// </summary>
    /// <param name="requestedSyncType">
    /// What the caller wants: Full re-pulls every worker; Differential uses the prior
    /// successful run's timestamp as the watermark. Differential silently degrades to Full when
    /// there's no prior successful run, or when the connector doesn't support incremental.
    /// </param>
    Task<Result> Run(SyncTriggerSource trigger, SyncType requestedSyncType, CancellationToken cancellationToken);

    /// <summary>
    /// Runs people sync for a single connection.
    /// </summary>
    /// <param name="requestedSyncType">See the all-connections overload.</param>
    Task<Result> Run(Guid connectionId, SyncTriggerSource trigger, SyncType requestedSyncType, CancellationToken cancellationToken);
}
