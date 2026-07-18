using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record CompletePokerSessionCommand(Guid Id) : ICommand;

public sealed class CompletePokerSessionCommandValidator : CustomValidator<CompletePokerSessionCommand>
{
    public CompletePokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class CompletePokerSessionCommandHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider, IPokerSessionNotifier notifier, ILogger<CompletePokerSessionCommandHandler> logger) : ICommandHandler<CompletePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<CompletePokerSessionCommandHandler> _logger = logger;

    public async Task<Result> Handle(CompletePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var result = session.Complete(_dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);
            await _notifier.NotifySessionCompleted(session.Id);

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
