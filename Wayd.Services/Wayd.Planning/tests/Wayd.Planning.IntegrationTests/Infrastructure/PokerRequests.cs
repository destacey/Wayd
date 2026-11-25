using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Result = CSharpFunctionalExtensions.Result;

namespace Wayd.Planning.IntegrationTests.Infrastructure;

/// <summary>
/// Runs a poker command through its real handler on a given context, as the seed's voter or facilitator, so a test can
/// race two requests against each other in a line apiece. A notifier left out is a mock nothing checks.
/// </summary>
internal static class PokerRequests
{
    /// <summary>Access that resolves every session to <paramref name="actor"/>.</summary>
    public static IPokerSessionAccess As(PokerSessionActor actor)
    {
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveActor(It.IsAny<PokerSession>(), It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        return access.Object;
    }

    /// <summary>The voter votes <paramref name="value"/> in the seed's round.</summary>
    public static Task<Result> Vote(WaydDbContext context, PokerSeed seed, string value, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new SubmitVoteCommandHandler(context, As(seed.Voter), SqlServerDbContextFixture.DateTimeProvider(), Mock.Of<ICurrentUser>(), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new SubmitVoteCommand(seed.SessionId, seed.RoundId, value), ct);

    /// <summary>The voter withdraws their vote in the seed's round.</summary>
    public static Task<Result> Withdraw(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new WithdrawVoteCommandHandler(context, As(seed.Voter), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new WithdrawVoteCommand(seed.SessionId, seed.RoundId), ct);

    /// <summary>The voter turns their voting off.</summary>
    public static Task<CSharpFunctionalExtensions.Result<PokerSessionMemberDto>> StopVoting(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new SetPokerSessionVotingCommandHandler(context, As(seed.Voter), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new SetPokerSessionVotingCommand(seed.SessionId, false), ct);

    /// <summary>The facilitator reveals the seed's round.</summary>
    public static Task<Result> Reveal(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new RevealPokerRoundCommandHandler(context, As(seed.Facilitator), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new RevealPokerRoundCommand(seed.SessionId, seed.RoundId), ct);

    /// <summary>The facilitator resets the seed's round.</summary>
    public static Task<Result> Reset(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new ResetPokerRoundCommandHandler(context, As(seed.Facilitator), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new ResetPokerRoundCommand(seed.SessionId, seed.RoundId), ct);

    /// <summary>The facilitator removes the seed's round.</summary>
    public static Task<Result> Remove(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new RemovePokerRoundCommandHandler(context, As(seed.Facilitator), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new RemovePokerRoundCommand(seed.SessionId, seed.RoundId), ct);

    /// <summary>The facilitator completes the session.</summary>
    public static Task<Result> Complete(WaydDbContext context, PokerSeed seed, CancellationToken ct, IPokerSessionNotifier? notifier = null) =>
        new CompletePokerSessionCommandHandler(context, As(seed.Facilitator), SqlServerDbContextFixture.DateTimeProvider(), notifier ?? Mock.Of<IPokerSessionNotifier>())
            .Handle(new CompletePokerSessionCommand(seed.SessionId), ct);

    /// <summary>The facilitator deletes the session.</summary>
    public static Task<Result> Delete(WaydDbContext context, PokerSeed seed, CancellationToken ct) =>
        new DeletePokerSessionCommandHandler(context, As(seed.Facilitator))
            .Handle(new DeletePokerSessionCommand(seed.SessionId), ct);
}
