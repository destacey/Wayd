using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record WithdrawVoteCommand(Guid SessionId, Guid RoundId) : ICommand, IRequireLinkedEmployee;

public sealed class WithdrawVoteCommandValidator : CustomValidator<WithdrawVoteCommand>
{
    public WithdrawVoteCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class WithdrawVoteCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<WithdrawVoteCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(WithdrawVoteCommand request, CancellationToken cancellationToken)
    {
        // Turning voting off, or a reset, can delete the vote first; the retry finds it gone and says so.
        var withdrawn = await PokerSessionRacingSave.RetryOnce(_planningDbContext, () => Withdraw(request, cancellationToken));
        if (withdrawn.IsFailure)
            return Result.Failure(withdrawn.Error);

        await _notifier.NotifyVoteWithdrawn(request.SessionId, request.RoundId, withdrawn.Value.UserId);

        return Result.Success();
    }

    private async Task<Result<PokerSessionActor>> Withdraw(WithdrawVoteCommand request, CancellationToken cancellationToken)
    {
        // A reveal of the round waits for this to commit, or this waits for the reveal and refuses.
        await using var unitOfWork = await _planningDbContext.BeginPokerRoundVoting(request.SessionId, request.RoundId, cancellationToken);

        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure<PokerSessionActor>("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.WithdrawVote(request.RoundId, actor);
        if (result.IsFailure)
            return Result.Failure<PokerSessionActor>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return actor;
    }
}
