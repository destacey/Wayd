namespace Wayd.AppIntegration.Application.Connections.Managers;

/// <summary>
/// Remembers which Azure DevOps work processes a sync run has already synced, across every
/// connection in the run. Azure DevOps's built-in processes share one id across organizations, so
/// connections to different organizations share one Wayd work process; without this, a run would
/// re-sync it once per connection.
/// </summary>
/// <remarks>
/// Registered as a singleton, because a run's connections may each get their own source instance.
/// Only the most recent <see cref="RunsKept"/> runs are kept, so it cannot grow without bound; a
/// run forgotten while still going costs one redundant, harmless re-sync of a process.
/// </remarks>
public sealed class AzureDevOpsWorkProcessSyncTracker
{
    private const int RunsKept = 20;

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, HashSet<Guid>> _runs = [];
    private readonly Queue<Guid> _runOrder = new();

    /// <summary>
    /// Records that <paramref name="syncId"/> has synced the process. True the first time for
    /// that run, false when the run has already synced it.
    /// </summary>
    public bool TryMarkSynced(Guid syncId, Guid workProcessExternalId)
    {
        lock (_lock)
        {
            if (!_runs.TryGetValue(syncId, out var processes))
            {
                processes = [];
                _runs[syncId] = processes;
                _runOrder.Enqueue(syncId);

                while (_runOrder.Count > RunsKept)
                    _runs.Remove(_runOrder.Dequeue());
            }

            return processes.Add(workProcessExternalId);
        }
    }
}
