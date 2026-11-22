using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record UpdatePokerRoundLabelCommand(Guid SessionId, Guid RoundId, string? Label) : ICommand, IRequireLinkedEmployee;

public sealed class UpdatePokerRoundLabelCommandValidator : CustomValidator<UpdatePokerRoundLabelCommand>
{
    public UpdatePokerRoundLabelCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
        RuleFor(c => c.Label).MaximumLength(512);
    }
}

public sealed class UpdatePokerRoundLabelCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<UpdatePokerRoundLabelCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(UpdatePokerRoundLabelCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.UpdateRoundLabel(request.RoundId, request.Label, actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyRoundLabelUpdated(session.Id, request.RoundId, request.Label);

        return Result.Success();
    }
}
