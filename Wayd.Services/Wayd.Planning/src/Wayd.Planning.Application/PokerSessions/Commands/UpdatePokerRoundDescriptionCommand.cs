using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Replaces a round's description; a null or blank description clears it.
/// </summary>
public sealed record UpdatePokerRoundDescriptionCommand(Guid SessionId, Guid RoundId, string? Description) : ICommand, IRequireLinkedEmployee;

/// <summary>
/// Requires both ids and holds the description to the column's length.
/// </summary>
public sealed class UpdatePokerRoundDescriptionCommandValidator : CustomValidator<UpdatePokerRoundDescriptionCommand>
{
    public UpdatePokerRoundDescriptionCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEmpty();
        RuleFor(c => c.Description).MaximumLength(PokerRound.DescriptionMaxLength);
    }
}

/// <summary>
/// Updates the round's description on an active session and tells the session's participants to refresh.
/// </summary>
public sealed class UpdatePokerRoundDescriptionCommandHandler(IPlanningDbContext planningDbContext, IPokerSessionAccess access, IPokerSessionNotifier notifier) : ICommandHandler<UpdatePokerRoundDescriptionCommand>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    public async Task<Result> Handle(UpdatePokerRoundDescriptionCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure("Poker session not found.");

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.UpdateRoundDescription(request.RoundId, request.Description, actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyRoundDescriptionUpdated(session.Id, request.RoundId);

        return Result.Success();
    }
}
