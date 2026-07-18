using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record UpdatePokerRoundLabelCommand(Guid SessionId, Guid RoundId, string? Label) : ICommand;

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

public sealed class UpdatePokerRoundLabelCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionNotifier notifier, ILogger<UpdatePokerRoundLabelCommandHandler> logger) : ICommandHandler<UpdatePokerRoundLabelCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<UpdatePokerRoundLabelCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdatePokerRoundLabelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .Include(s => s.Rounds)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.UpdateRoundLabel(request.RoundId, request.Label);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyRoundLabelUpdated(session.Id, request.RoundId, request.Label);

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
