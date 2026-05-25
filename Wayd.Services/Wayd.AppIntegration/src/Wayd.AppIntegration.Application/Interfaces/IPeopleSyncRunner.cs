namespace Wayd.AppIntegration.Application.Interfaces;

public interface IPeopleSyncRunner : ITransientService
{
    /// <summary>
    /// Runs people sync across all active PeopleSync-category connections.
    /// </summary>
    Task<Result> Run(SyncTriggerSource trigger, CancellationToken cancellationToken);

    /// <summary>
    /// Runs people sync for a single connection.
    /// </summary>
    Task<Result> Run(Guid connectionId, SyncTriggerSource trigger, CancellationToken cancellationToken);
}
