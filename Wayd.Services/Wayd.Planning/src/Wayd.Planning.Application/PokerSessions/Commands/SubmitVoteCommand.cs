using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record SubmitVoteCommand(Guid SessionId, Guid RoundId, string Value) : ICommand, IRequireLinkedEmployee;

public sealed class SubmitVoteCommandValidator : CustomValidator<SubmitVoteCommand>
{
    public SubmitVoteCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
        RuleFor(c => c.Value).NotEmpty().MaximumLength(32);
    }
}

public sealed class SubmitVoteCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, IPokerSessionNotifier notifier) : ICommandHandler<SubmitVoteCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(SubmitVoteCommand request, CancellationToken cancellationToken)
    {
        // A double-submitted first vote meets the earlier request's vote, or member row, at a unique index; a vote
        // racing a change to voting finds the member row changed, and the retry sees whether they still vote.
        var submitted = await PokerSessionRacingSave.RetryOnce(_planningDbContext, () => Submit(request, cancellationToken));
        if (submitted.IsFailure)
            return Result.Failure(submitted.Error);

        var (session, actor, joinedSession) = submitted.Value;
        if (joinedSession)
            await _notifier.NotifyMemberUpdated(session.Id, await _planningDbContext.MemberDto(session.FindMember(actor.EmployeeId)!, cancellationToken));

        var participantName = currentUser.Name ?? "Unknown";
        await _notifier.NotifyVoteSubmitted(session.Id, request.RoundId, actor.UserId, participantName);

        return Result.Success();
    }

    private async Task<Result<(PokerSession Session, PokerSessionActor Actor, bool JoinedSession)>> Submit(SubmitVoteCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Members)
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure<(PokerSession, PokerSessionActor, bool)>("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var version = await _planningDbContext.EstimationScaleVersions
            .AsNoTracking()
            .FirstAsync(v => v.Id == session.EstimationScaleVersionId, cancellationToken);

        var result = session.SubmitVote(request.RoundId, request.Value, version, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return Result.Failure<(PokerSession, PokerSessionActor, bool)>(result.Error);

        if (!result.Value)
            PokerSessionRacingSave.TouchMember(_planningDbContext, session.FindMember(actor.EmployeeId)!);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return (session, actor, result.Value);
    }
}
