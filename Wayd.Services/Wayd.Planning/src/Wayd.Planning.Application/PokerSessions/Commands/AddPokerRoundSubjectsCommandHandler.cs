using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Checks the session to add to and the records to add before the handler loads anything.
/// </summary>
public sealed class AddPokerRoundSubjectsCommandValidator : CustomValidator<AddPokerRoundSubjectsCommand>
{
    public AddPokerRoundSubjectsCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.HostKind).IsInEnum();
        RuleFor(c => c.HostId).NotEmpty();
        RuleFor(c => c.Subjects)
            .NotEmpty()
            .Must(s => s.Count <= PokerRoundSubjectInputValidator.MaxSubjectsPerRequest)
                .WithMessage($"Add at most {PokerRoundSubjectInputValidator.MaxSubjectsPerRequest} records at a time.");
        RuleForEach(c => c.Subjects).SetValidator(new PokerRoundSubjectInputValidator());
    }
}

/// <summary>
/// Adds a round about each record to a session of the named host, as one of the people who run it, and tells the
/// session's connections about each.
/// </summary>
public sealed class AddPokerRoundSubjectsCommandHandler(
    IPlanningDbContext planningDbContext,
    IPokerSessionAccess access,
    IPokerSessionNotifier notifier) : ICommandHandler<AddPokerRoundSubjectsCommand>
{
    /// <summary>
    /// The failure for a session that is not hosted by the host the records were checked against.
    /// </summary>
    public const string NotThisHostsSessionError = "This session does not belong to that area.";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IPokerSessionNotifier _notifier = notifier;

    /// <summary>
    /// Refuses a session of another host or none before asking who the caller is, then lets the session decide.
    /// </summary>
    public async Task<Result> Handle(AddPokerRoundSubjectsCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure(PokerSession.NotFoundError);

        if (session.Host != new PokerSessionHost(request.HostKind, request.HostId))
            return Result.Failure(NotThisHostsSessionError);

        var actor = await _access.ResolveActor(session, cancellationToken);

        var result = session.AddSubjectRounds(request.Subjects.Select(PokerRoundSubjectInputs.ToRecord), actor);
        if (result.IsFailure)
            return Result.Failure(result.Error);

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        foreach (var round in result.Value)
            await _notifier.NotifyRoundAdded(session.Id, round.Adapt<PokerRoundDto>());

        return Result.Success();
    }
}
