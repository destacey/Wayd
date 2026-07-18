using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record RemovePokerRoundCommand(Guid SessionId, Guid RoundId) : ICommand;

public sealed class RemovePokerRoundCommandValidator : CustomValidator<RemovePokerRoundCommand>
{
    public RemovePokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class RemovePokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionNotifier notifier, ILogger<RemovePokerRoundCommandHandler> logger) : ICommandHandler<RemovePokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<RemovePokerRoundCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemovePokerRoundCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.RemoveRound(request.RoundId);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyRoundRemoved(session.Id, request.RoundId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
