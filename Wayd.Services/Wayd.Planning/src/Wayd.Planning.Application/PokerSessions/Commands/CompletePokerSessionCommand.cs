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
        await _notifier.NotifySessionCompleted(session.Id);

        return Result.Success();
    }
}
