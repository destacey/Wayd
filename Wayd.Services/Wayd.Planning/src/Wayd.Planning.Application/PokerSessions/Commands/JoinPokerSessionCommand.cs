using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Join an active poker session, voting unless told otherwise. Joining again changes nothing.
/// </summary>
public sealed record JoinPokerSessionCommand(Guid Id, bool IsVoting = true) : ICommand<PokerSessionMemberDto>, IRequireLinkedEmployee;

public sealed class JoinPokerSessionCommandValidator : CustomValidator<JoinPokerSessionCommand>
{
    public JoinPokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class JoinPokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, IPokerSessionNotifier notifier) : ICommandHandler<JoinPokerSessionCommand, PokerSessionMemberDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result<PokerSessionMemberDto>> Handle(JoinPokerSessionCommand request, CancellationToken cancellationToken)
    {
        // A double join meets the earlier request's member row at a unique index; the retry finds it and joins nobody.
        var joined = await PokerSessionDuplicateSave.RetryOnce(_planningDbContext, () => Join(request, cancellationToken));
        if (joined.IsFailure)
            return Result.Failure<PokerSessionMemberDto>(joined.Error);

        var (actor, change) = joined.Value;
        return await _notifier.NotifyMembershipChanged(_planningDbContext, request.Id, actor, change, cancellationToken);
    }

    private async Task<Result<(PokerSessionActor Actor, PokerSessionMembershipChange Change)>> Join(JoinPokerSessionCommand request, CancellationToken cancellationToken)
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

        var result = session.Join(request.IsVoting, actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return Result.Failure<(PokerSessionActor, PokerSessionMembershipChange)>(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        return (actor, result.Value);
    }
}
