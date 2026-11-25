using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record ResetPokerRoundCommand(Guid SessionId, Guid RoundId) : ICommand, IRequireLinkedEmployee;

public sealed class ResetPokerRoundCommandValidator : CustomValidator<ResetPokerRoundCommand>
{
    public ResetPokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class ResetPokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<ResetPokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(ResetPokerRoundCommand request, CancellationToken cancellationToken)
    {
        // Taken before the load, so the reset clears every vote that committed first and none can land on the
        // round it is clearing.
        await using var unitOfWork = await _planningDbContext.BeginExclusivePokerRoundChange(request.SessionId, request.RoundId, cancellationToken);

        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.ResetRound(request.RoundId, actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        await _notifier.NotifyRoundReset(session.Id, request.RoundId);

        return Result.Success();
    }
}
