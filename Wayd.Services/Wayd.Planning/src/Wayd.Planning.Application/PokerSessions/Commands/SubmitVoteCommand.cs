using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record SubmitVoteCommand(Guid SessionId, Guid RoundId, string Value) : ICommand;

public sealed class SubmitVoteCommandValidator : CustomValidator<SubmitVoteCommand>
{
    public SubmitVoteCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
        RuleFor(c => c.Value).NotEmpty().MaximumLength(32);
    }
}

internal sealed class SubmitVoteCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, IPokerSessionNotifier notifier, ILogger<SubmitVoteCommandHandler> logger) : ICommandHandler<SubmitVoteCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SubmitVoteCommandHandler> _logger = logger;
    private readonly string _currentUserId = currentUser.GetUserId();

    public async Task<Result> Handle(SubmitVoteCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.SubmitVote(request.RoundId, _currentUserId, request.Value, _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var participantName = currentUser.Name ?? "Unknown";
            await _notifier.NotifyVoteSubmitted(session.Id, request.RoundId, _currentUserId, participantName);

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
