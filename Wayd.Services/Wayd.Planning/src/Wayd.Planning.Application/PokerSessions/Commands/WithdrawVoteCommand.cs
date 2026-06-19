using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record WithdrawVoteCommand(Guid SessionId, Guid RoundId) : ICommand;

public sealed class WithdrawVoteCommandValidator : CustomValidator<WithdrawVoteCommand>
{
    public WithdrawVoteCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
    }
}

internal sealed class WithdrawVoteCommandHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser, IPokerSessionNotifier notifier, ILogger<WithdrawVoteCommandHandler> logger) : ICommandHandler<WithdrawVoteCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<WithdrawVoteCommandHandler> _logger = logger;
    private readonly string _currentUserId = currentUser.GetUserId();

    public async Task<Result> Handle(WithdrawVoteCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.WithdrawVote(request.RoundId, _currentUserId);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyVoteWithdrawn(session.Id, request.RoundId, _currentUserId);

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
