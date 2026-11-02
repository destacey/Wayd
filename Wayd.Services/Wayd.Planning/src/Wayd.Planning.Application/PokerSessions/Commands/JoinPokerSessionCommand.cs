using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Join an active poker session, voting unless told otherwise. Joining again changes nothing.
/// </summary>
public sealed record JoinPokerSessionCommand(Guid Id, bool IsVoting = true) : ICommand<PokerSessionMemberDto>, IRequireLinkedEmployee;

public sealed class JoinPokerSessionCommandValidator : CustomValidator<JoinPokerSessionCommand>
{
    public JoinPokerSessionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class JoinPokerSessionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, IPokerSessionNotifier notifier, ILogger<JoinPokerSessionCommandHandler> logger) : ICommandHandler<JoinPokerSessionCommand, PokerSessionMemberDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<JoinPokerSessionCommandHandler> _logger = logger;

    public async Task<Result<PokerSessionMemberDto>> Handle(JoinPokerSessionCommand request, CancellationToken cancellationToken)
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
                return Result.Failure<PokerSessionMemberDto>("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var result = session.Join(request.IsVoting, actor, _dateTimeProvider.Now);
            if (result.IsFailure)
                return Result.Failure<PokerSessionMemberDto>(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            return await _notifier.NotifyMembershipChanged(_planningDbContext, session.Id, actor, result.Value, cancellationToken);
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", requestName, request);
            return Result.Failure<PokerSessionMemberDto>($"Wayd Request: Exception for Request {requestName} {request}");
        }
    }
}
