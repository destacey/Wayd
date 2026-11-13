using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// An <see cref="IPokerSessionAccess"/> that answers with whoever the test sets, so handler tests choose the
/// caller without the identity and organization lookups behind the real one. The session still decides.
/// </summary>
public sealed class FakePokerSessionAccess : IPokerSessionAccess
{
    /// <summary>The caller commands act as; a generated session's facilitator unless set.</summary>
    public PokerSessionActor Actor { get; set; } = PokerSessionActors.Facilitator;

    /// <summary>The caller queries read as; the facilitator unless set.</summary>
    public PokerSessionReader Reader { get; set; } =
        new(PokerSessionActors.FacilitatorEmployeeId, false, [], false);

    /// <summary>What <see cref="CanUserAccess"/> answers.</summary>
    public bool UserAccess { get; set; } = true;

    /// <summary>The hosts <see cref="ManagesHost"/> says the caller manages; none unless set.</summary>
    public HashSet<Guid> ManagedHostIds { get; } = [];

    public Task<PokerSessionActor> ResolveActor(PokerSession session, CancellationToken cancellationToken) =>
        Task.FromResult(Actor);

    public Task<PokerSessionReader> ResolveReader(CancellationToken cancellationToken) =>
        Task.FromResult(Reader);

    public Task<bool> CanUserAccess(Guid sessionId, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(UserAccess);

    public Task<bool> ManagesHost(PokerSessionHost? host, Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(host is not null && ManagedHostIds.Contains(host.Id));
}
