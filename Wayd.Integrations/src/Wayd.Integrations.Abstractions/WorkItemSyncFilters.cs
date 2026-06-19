namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Opaque, connector-specific filter payload travelling inside a <see cref="WorkspaceSyncTarget"/>.
/// The runner never inspects the contents — it just hands the filters back to the source on each
/// per-workspace sync call. Each source defines its own well-known keys (e.g. <c>"azdo.teamSettings"</c>).
/// </summary>
public sealed class WorkItemSyncFilters
{
    public static readonly WorkItemSyncFilters Empty = new();

    private readonly Dictionary<string, string> _payload;

    public WorkItemSyncFilters(IReadOnlyDictionary<string, string>? payload = null)
    {
        _payload = payload is null
            ? new Dictionary<string, string>()
            : payload.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public bool TryGet(string key, out string? value)
    {
        var found = _payload.TryGetValue(key, out var v);
        value = found ? v : null;
        return found;
    }

    public IReadOnlyDictionary<string, string> AsDictionary => _payload;
}
