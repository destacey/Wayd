using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings;

/// <summary>
/// Reads and saves system settings sections. Reads are cached; a save clears its section's cache entry.
/// </summary>
public interface ISystemSettingsStore
{
    /// <inheritdoc cref="ISettings{TSection}.Get"/>
    Task<TSection> Get<TSection>(CancellationToken cancellationToken)
        where TSection : class, ISettingsSection<TSection>, new();

    /// <summary>
    /// Replaces the section's values after validating them with the section's validator. Saving the values
    /// already in effect changes nothing and succeeds. Fails when another save of the section got in first.
    /// </summary>
    Task<Result> Save<TSection>(TSection values, EventActor actor, CancellationToken cancellationToken)
        where TSection : class, ISettingsSection<TSection>, new();
}
