using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Points a poker session at a round, which everyone following the facilitator is then shown.
/// </summary>
/// <param name="SessionId">The session to point.</param>
/// <param name="RoundId">The round to show followers, or null to return them to the round in play.</param>
public sealed record FocusPokerRoundCommand(Guid SessionId, Guid? RoundId) : ICommand, IRequireLinkedEmployee;

public sealed class FocusPokerRoundCommandValidator : CustomValidator<FocusPokerRoundCommand>
{
    public FocusPokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEqual(Guid.Empty);
    }
}

public sealed class FocusPokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<FocusPokerRoundCommandHandler> logger) : ICommandHandler<FocusPokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<FocusPokerRoundCommandHandler> _logger = logger;

    public async Task<Result> Handle(FocusPokerRoundCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var previousRoundId = session.FocusedRoundId;

            var result = session.FocusRound(request.RoundId, actor);
            if (result.IsFailure)
                return result;

            // Selecting the round already shown tells the session nothing, and a broadcast makes every
            // participant read the session again.
            if (session.FocusedRoundId == previousRoundId)
                return Result.Success();

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyRoundFocused(session.Id, session.FocusedRoundId);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
