using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record RemovePokerRoundCommand(Guid SessionId, Guid RoundId) : ICommand, IRequireLinkedEmployee;

public sealed class RemovePokerRoundCommandValidator : CustomValidator<RemovePokerRoundCommand>
{
    public RemovePokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class RemovePokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<RemovePokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(RemovePokerRoundCommand request, CancellationToken cancellationToken)
    {
        // A vote saving into the round would otherwise insert against a round this deletes, and fail on its foreign
        // key; with the lock it commits first and is deleted with the round, or waits and finds the round gone.
        await using var unitOfWork = await _planningDbContext.BeginExclusivePokerRoundChange(request.SessionId, request.RoundId, cancellationToken);

        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.RemoveRound(request.RoundId, actor);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        await _notifier.NotifyRoundRemoved(session.Id, request.RoundId);

        return Result.Success();
    }
}
