using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record CompletePokerSessionCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class CompletePokerSessionCommandValidator : CustomValidator<CompletePokerSessionCommand>
{
    public CompletePokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class CompletePokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, IPokerSessionNotifier notifier) : ICommandHandler<CompletePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(CompletePokerSessionCommand request, CancellationToken cancellationToken)
    {
        // Every round the session has when this starts, so a vote saved into one this reads as empty is not deleted
        // with it, and none lands in one after the session is completed: it commits first and keeps its round, or
        // waits and is refused. A round added after the lock is taken is not held by it.
        await using var unitOfWork = await _planningDbContext.BeginExclusivePokerRoundChange(request.Id, null, cancellationToken);

        // Completing deletes empty rounds, and a round reads as empty unless its votes are loaded.
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.Complete(actor, _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        await _notifier.NotifySessionCompleted(session.Id);

        return Result.Success();
    }
}
