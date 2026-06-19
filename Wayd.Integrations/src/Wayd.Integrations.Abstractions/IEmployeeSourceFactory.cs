using Wayd.Common.Application.Interfaces;

namespace Wayd.Integrations.Abstractions;

/// <summary>
/// Resolves an <see cref="IEmployeeSource"/> for a given connection. Returns a failure result
/// when no source is registered for the descriptor's connector — the runner records this as a
/// failed run rather than throwing.
/// </summary>
public interface IEmployeeSourceFactory : IScopedService
{
    Result<IEmployeeSource> Create(SyncableConnectionDescriptor descriptor);
}
