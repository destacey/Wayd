using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.Planning;

/// <summary>
/// Answers Planning, for one kind of host, which hosts an employee manages. Whoever manages a host facilitates
/// every poker session it hosts, alongside the session's own facilitators.
/// </summary>
/// <remarks>
/// Implemented by the module that owns the host, once per <see cref="Kind"/>, and registered through the
/// <see cref="IScopedService"/> scan. Planning never reads that module's records itself. A kind with no
/// implementation manages nothing, so its sessions are run only by their own facilitators and poker
/// administrators, and none can be started.
/// <para>
/// Planning reads the answer when a connection joins a session's live updates, so the implementing module must
/// recheck its users' connections (<c>RealtimeAccessRevoker.RecheckUsers</c>) when someone stops managing a host,
/// or they keep receiving its sessions' updates until they reconnect.
/// </para>
/// </remarks>
public interface IPokerSessionHostAccess : IScopedService
{
    /// <summary>The kind of host this answers for.</summary>
    PokerSessionHostKind Kind { get; }

    /// <summary>
    /// The hosts of <see cref="Kind"/> the employee manages. An inactive employee manages none.
    /// </summary>
    Task<IReadOnlySet<Guid>> HostsManagedBy(Guid employeeId, CancellationToken cancellationToken);
}
