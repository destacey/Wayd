namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record DeletePokerSessionCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class DeletePokerSessionCommandValidator : CustomValidator<DeletePokerSessionCommand>
{
    public DeletePokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeletePokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access) : ICommandHandler<DeletePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;

    public async Task<Result> Handle(DeletePokerSessionCommand request, CancellationToken cancellationToken)
    {
        // A vote saving into one of the session's rounds would otherwise insert against a round this deletes, and fail
        // on its foreign key; with the lock it commits first and is deleted with the session, or waits and finds the
        // session gone.
        await using var unitOfWork = await _planningDbContext.BeginExclusivePokerRoundChange(request.Id, null, cancellationToken);

        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var canDelete = session.CanDelete(actor);
        if (canDelete.IsFailure)
            return canDelete;

        _planningDbContext.PokerSessions.Remove(session);
        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
