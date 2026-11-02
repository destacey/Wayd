using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

public sealed record SubmitVoteCommand(Guid SessionId, Guid RoundId, string Value) : ICommand, IRequireLinkedEmployee;

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

public sealed class SubmitVoteCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IDateTimeProvider dateTimeProvider, ICurrentUser currentUser, IPokerSessionNotifier notifier, ILogger<SubmitVoteCommandHandler> logger) : ICommandHandler<SubmitVoteCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SubmitVoteCommandHandler> _logger = logger;

    public async Task<Result> Handle(SubmitVoteCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .Include(s => s.Members)
                .Include(s => s.Rounds)
                    .ThenInclude(r => r.Votes)
                .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var version = await _planningDbContext.EstimationScaleVersions
                .AsNoTracking()
                .FirstAsync(v => v.Id == session.EstimationScaleVersionId, cancellationToken);

            var result = session.SubmitVote(request.RoundId, request.Value, version, actor, _dateTimeProvider.Now);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            var joinedSession = result.Value;
            if (joinedSession)
                await _notifier.NotifyMemberUpdated(session.Id, await _planningDbContext.MemberDto(session.FindMember(actor.EmployeeId)!, cancellationToken));

            var participantName = currentUser.Name ?? "Unknown";
            await _notifier.NotifyVoteSubmitted(session.Id, request.RoundId, actor.UserId, participantName);

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
