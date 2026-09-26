using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings;

/// <summary>
/// The system settings in effect for one section. Modules depend on this and the section record, never on
/// section keys or the stored JSON.
/// </summary>
public interface ISettings<TSection>
    where TSection : class, ISettingsSection<TSection>, new()
{
    /// <summary>The section's values, with the code defaults for anything never saved.</summary>
    Task<TSection> Get(CancellationToken cancellationToken);
}

// Public so Wolverine's codegen can construct it inline for a handler that takes ISettings<T>.
public sealed class StoredSettings<TSection>(ISystemSettingsStore store) : ISettings<TSection>
    where TSection : class, ISettingsSection<TSection>, new()
{
    private readonly ISystemSettingsStore _store = store;

    public Task<TSection> Get(CancellationToken cancellationToken) => _store.Get<TSection>(cancellationToken);
}
