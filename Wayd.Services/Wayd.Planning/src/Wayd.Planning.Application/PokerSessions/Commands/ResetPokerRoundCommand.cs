using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record ResetPokerRoundCommand(Guid SessionId, Guid RoundId) : ICommand;

public sealed class ResetPokerRoundCommandValidator : CustomValidator<ResetPokerRoundCommand>
{
    public ResetPokerRoundCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

public sealed class ResetPokerRoundCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionNotifier notifier, ILogger<ResetPokerRoundCommandHandler> logger) : ICommandHandler<ResetPokerRoundCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<ResetPokerRoundCommandHandler> _logger = logger;

    public async Task<Result> Handle(ResetPokerRoundCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.ResetRound(request.RoundId);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyRoundReset(session.Id, request.RoundId);

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
