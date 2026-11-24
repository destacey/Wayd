using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Turn the caller's voting on or off in a poker session they are in. Turning it off withdraws their votes in rounds
/// that have not been revealed.
/// </summary>
public sealed record SetPokerSessionVotingCommand(Guid Id, bool IsVoting) : ICommand<PokerSessionMemberDto>, IRequireLinkedEmployee;

public sealed class SetPokerSessionVotingCommandValidator : CustomValidator<SetPokerSessionVotingCommand>
{
    public SetPokerSessionVotingCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class SetPokerSessionVotingCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<SetPokerSessionVotingCommand, PokerSessionMemberDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result<PokerSessionMemberDto>> Handle(SetPokerSessionVotingCommand request, CancellationToken cancellationToken)
    {
        // A vote saved since the session was read changed the member row; the retry sees the vote and withdraws it.
        var set = await PokerSessionRacingSave.RetryOnce(_planningDbContext, () => Set(request, cancellationToken));
        if (set.IsFailure)
            return Result.Failure<PokerSessionMemberDto>(set.Error);

        var (actor, change) = set.Value;
        return await _notifier.NotifyMembershipChanged(_planningDbContext, request.Id, actor, change, cancellationToken);
    }

    private async Task<Result<(PokerSessionActor Actor, PokerSessionMembershipChange Change)>> Set(SetPokerSessionVotingCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Members)
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return Result.Failure<(PokerSessionActor, PokerSessionMembershipChange)>("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.SetVoting(request.IsVoting, actor);
        if (result.IsFailure)
            return Result.Failure<(PokerSessionActor, PokerSessionMembershipChange)>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return (actor, result.Value);
    }
}
