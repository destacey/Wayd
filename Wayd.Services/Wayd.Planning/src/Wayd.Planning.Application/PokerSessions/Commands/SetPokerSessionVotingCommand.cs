using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Turn the caller's voting on or off in a poker session they are in. Turning it off withdraws their votes in rounds
/// that have not been revealed.
/// </summary>
public sealed record SetPokerSessionVotingCommand(Guid Id, bool IsVoting) : ICommand<PokerSessionMemberDto>, IRequireLinkedEmployee;

public sealed class SetPokerSessionVotingCommandValidator : CustomValidator<SetPokerSessionVotingCommand>
{
    public SetPokerSessionVotingCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class SetPokerSessionVotingCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<SetPokerSessionVotingCommandHandler> logger) : ICommandHandler<SetPokerSessionVotingCommand, PokerSessionMemberDto>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SetPokerSessionVotingCommandHandler> _logger = logger;

    public async Task<Result<PokerSessionMemberDto>> Handle(SetPokerSessionVotingCommand request, CancellationToken cancellationToken)
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

            var result = session.SetVoting(request.IsVoting, actor);
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
