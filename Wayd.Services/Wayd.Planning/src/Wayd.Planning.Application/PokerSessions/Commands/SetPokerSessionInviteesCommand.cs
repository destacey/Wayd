using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Replace the employees and teams invited to a poker session. Invites admit people only while the session is
/// private. Its facilitators and poker administrators can, on an active or a completed session.
/// </summary>
public sealed record SetPokerSessionInviteesCommand(Guid Id, IReadOnlyCollection<Guid> EmployeeIds, IReadOnlyCollection<Guid> TeamIds) : ICommand, IRequireLinkedEmployee;

public sealed class SetPokerSessionInviteesCommandValidator : CustomValidator<SetPokerSessionInviteesCommand>
{
    public SetPokerSessionInviteesCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeIds).NotNull();
        RuleFor(c => c.TeamIds).NotNull();
        RuleForEach(c => c.EmployeeIds).NotEmpty();
        RuleForEach(c => c.TeamIds).NotEmpty();
    }
}

public sealed class SetPokerSessionInviteesCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<SetPokerSessionInviteesCommandHandler> logger) : ICommandHandler<SetPokerSessionInviteesCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SetPokerSessionInviteesCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetPokerSessionInviteesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            // Checked before the invitees, so a caller who cannot run the session learns nothing about them.
            if (!session.CanManage(actor))
                return session.SetInvitees(request.EmployeeIds, request.TeamIds, actor);

            var exist = Result.Combine(
                await _planningDbContext.EnsureEmployeesExist(request.EmployeeIds, cancellationToken),
                await _planningDbContext.EnsureTeamsExist(request.TeamIds, cancellationToken));
            if (exist.IsFailure)
                return exist;

            var result = session.SetInvitees(request.EmployeeIds, request.TeamIds, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAccessChanged(session.Id);

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
