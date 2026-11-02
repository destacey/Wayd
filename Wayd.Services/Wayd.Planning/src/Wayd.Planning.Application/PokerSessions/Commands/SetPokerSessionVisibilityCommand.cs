using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Interfaces;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Make a poker session public or private. Its facilitators and poker administrators can, on an active or a
/// completed session.
/// </summary>
public sealed record SetPokerSessionVisibilityCommand(Guid Id, Visibility Visibility) : ICommand, IRequireLinkedEmployee;

public sealed class SetPokerSessionVisibilityCommandValidator : CustomValidator<SetPokerSessionVisibilityCommand>
{
    public SetPokerSessionVisibilityCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Visibility).IsInEnum();
    }
}

public sealed class SetPokerSessionVisibilityCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier, ILogger<SetPokerSessionVisibilityCommandHandler> logger) : ICommandHandler<SetPokerSessionVisibilityCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;
    private readonly ILogger<SetPokerSessionVisibilityCommandHandler> _logger = logger;

    public async Task<Result> Handle(SetPokerSessionVisibilityCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await _planningDbContext.PokerSessions
                .IncludeAccess()
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

            if (session is null)
                return Result.Failure("Poker session not found.");

            var actor = await _access.ResolveActor(session, cancellationToken);

            var result = session.SetVisibility(request.Visibility, actor);
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
