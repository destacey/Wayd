using Wayd.Common.Application.Interfaces;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Resolves an <see cref="IWorkItemSource"/> for a given connection. Returns a failure result
/// when no source is registered for the descriptor's connector — the runner treats this as
/// "skip this connection" rather than a fatal error.
/// </summary>
public interface IWorkItemSourceFactory : IScopedService
{
    Result<IWorkItemSource> Create(SyncableConnectionDescriptor descriptor);
}
