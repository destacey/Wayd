using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record SetConsensusCommand(Guid SessionId, Guid RoundId, string Estimate) : ICommand, IRequireLinkedEmployee;

public sealed class SetConsensusCommandValidator : CustomValidator<SetConsensusCommand>
{
    public SetConsensusCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
        RuleFor(c => c.Estimate).NotEmpty().MaximumLength(32);
    }
}

public sealed class SetConsensusCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<SetConsensusCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(SetConsensusCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var version = await _planningDbContext.EstimationScaleVersions
            .AsNoTracking()
            .FirstAsync(v => v.Id == session.EstimationScaleVersionId, cancellationToken);

        var result = session.SetConsensus(request.RoundId, request.Estimate, version, actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyConsensusSet(session.Id, request.RoundId, result.Value.ConsensusEstimate!);

        return Result.Success();
    }
}
