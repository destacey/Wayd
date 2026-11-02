using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Leave an active poker session. The caller stays in its record of who took part; their votes in rounds that
/// have not been revealed are withdrawn.
/// </summary>
public sealed record LeavePokerSessionCommand(Guid Id) : ICommand, IRequireLinkedEmployee;

public sealed class LeavePokerSessionCommandValidator : CustomValidator<LeavePokerSessionCommand>
{
    public LeavePokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class LeavePokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, IPokerSessionNotifier notifier, ILogger<LeavePokerSessionCommandHandler> logger) : ICommandHandler<LeavePokerSessionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<LeavePokerSessionCommandHandler> _logger = logger;

    public async Task<Result> Handle(LeavePokerSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .Include(s => s.Members)
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var result = session.Leave(actor, _dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyMembershipChanged(_planningDbContext, session.Id, actor, result.Value, cancellationToken);

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
