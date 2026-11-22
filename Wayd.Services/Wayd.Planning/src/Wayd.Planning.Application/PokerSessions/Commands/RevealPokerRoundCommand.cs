using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record RevealPokerRoundCommand(Guid SessionId, Guid RoundId) : ICommand, IRequireLinkedEmployee;

public sealed class RevealPokerRoundCommandValidator : CustomValidator<RevealPokerRoundCommand>
{
    public RevealPokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class RevealPokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<RevealPokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(RevealPokerRoundCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
                    .ThenInclude(v => v.Participant)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.RevealRound(request.RoundId, actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        var voteDtos = result.Value.Votes.Adapt<List<PokerVoteDto>>();
        await _notifier.NotifyVotesRevealed(session.Id, result.Value.Id, voteDtos);

        return Result.Success();
    }
}
