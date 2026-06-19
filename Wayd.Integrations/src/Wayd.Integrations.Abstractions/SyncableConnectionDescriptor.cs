namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Connector-neutral snapshot of a syncable connection handed to an <see cref="IWorkItemSource"/>
/// via <see cref="IWorkItemSourceFactory.Create"/>. Carries the boxed connector-specific
/// configuration and team configuration — each source casts to the type it knows.
/// </summary>
public sealed record SyncableConnectionDescriptor(
    Guid ConnectionId,
    Connector Connector,
    string? SystemId,
    object Configuration,
    object? TeamConfiguration);
