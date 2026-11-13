using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Replace a poker session's facilitators; at least one is required. Its facilitators and poker administrators
/// can, on an active or a completed session.
/// </summary>
public sealed record SetPokerSessionFacilitatorsCommand(Guid Id, IReadOnlyCollection<Guid> EmployeeIds) : ICommand, IRequireLinkedEmployee;

public sealed class SetPokerSessionFacilitatorsCommandValidator : CustomValidator<SetPokerSessionFacilitatorsCommand>
{
    public SetPokerSessionFacilitatorsCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.EmployeeIds)
            .NotEmpty()
            .WithMessage("A poker session needs at least one facilitator.");
        RuleForEach(c => c.EmployeeIds).NotEmpty();
    }
}

public sealed class SetPokerSessionFacilitatorsCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<SetPokerSessionFacilitatorsCommandHandler> logger) : ICommandHandler<SetPokerSessionFacilitatorsCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SetPokerSessionFacilitatorsCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetPokerSessionFacilitatorsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            // Checked before the employees, so a caller who cannot run the session learns nothing about them.
            if (!session.CanManage(actor))
                return session.SetFacilitators(request.EmployeeIds, actor);

            var exist = await _planningDbContext.EnsureEmployeesExist(request.EmployeeIds, cancellationToken);
            if (exist.IsFailure)
                return exist;

            var result = session.SetFacilitators(request.EmployeeIds, actor);
            if (result.IsFailure)
                return result;

            await _planningDbContext.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAccessChanged(session.Id);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not ForbiddenException)
        {
            var requestName = request.GetType().Name;
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name}", requestName);
            return Result.Failure($"Wayd Request: Exception for Request {requestName}");
        }
    }
}
