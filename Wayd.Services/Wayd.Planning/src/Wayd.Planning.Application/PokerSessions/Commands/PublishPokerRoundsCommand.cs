using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Planning.Application.PokerSessions.Commands;

/// <summary>
/// Publishes the accepted consensus of a poker round, or of every round that can be published, to the record each
/// estimates, and records on each round whether its record's area took it.
/// </summary>
/// <remarks>
/// A facilitator's action, on an active or a completed session. The record's area decides again, as the caller,
/// whether it takes the consensus; a refusal writes nothing and is kept on the round with its reason, so the
/// command succeeds whenever the rounds could be sent, and its answer says what came of each.
/// </remarks>
/// <param name="SessionId">The session.</param>
/// <param name="RoundId">The round to publish, or null for every accepted round about a record not yet published.</param>
public sealed record PublishPokerRoundsCommand(Guid SessionId, Guid? RoundId = null)
    : ICommand<IReadOnlyList<PokerRoundDto>>, IRequireLinkedEmployee;

/// <summary>
/// Checks the session and round are named before the handler loads anything.
/// </summary>
public sealed class PublishPokerRoundsCommandValidator : CustomValidator<PublishPokerRoundsCommand>
{
    public PublishPokerRoundsCommandValidator()
    {
        RuleFor(c => c.SessionId).NotEmpty();
        RuleFor(c => c.RoundId).NotEqual(Guid.Empty);
    }
}

/// <summary>
/// Lets the session decide which rounds the caller may publish, sends each to the module that owns its record, and
/// keeps that module's answer on the round.
/// </summary>
public sealed class PublishPokerRoundsCommandHandler(
    IPlanningDbContext planningDbContext,
    IPokerSessionAccess access,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider,
    IPokerSessionNotifier notifier) : ICommandHandler<PublishPokerRoundsCommand, IReadOnlyList<PokerRoundDto>>
{
    /// <summary>
    /// The refusal kept on a round whose session has no host of the kind its record needs, so no area can take it.
    /// </summary>
    public const string NoPublisherError = "This session cannot publish to that kind of record.";

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IPokerSessionAccess _access = access;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IPokerSessionNotifier _notifier = notifier;

    /// <summary>
    /// Publishes each round in turn. The record's module saves its own change; the round's outcome is saved once
    /// every round has been sent, so a failure between the two leaves a round to publish again, which writes
    /// nothing new.
    /// </summary>
    public async Task<Result<IReadOnlyList<PokerRoundDto>>> Handle(PublishPokerRoundsCommand request, CancellationToken cancellationToken)
    {
        var session = await _planningDbContext.PokerSessions
            .IncludeAccess()
            .Include(s => s.Rounds)
                .ThenInclude(r => r.Votes)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null)
            return Result.Failure<IReadOnlyList<PokerRoundDto>>(PokerSession.NotFoundError);

        var actor = await _access.ResolveActor(session, cancellationToken);

        var rounds = session.RoundsToPublish(request.RoundId, actor);
        if (rounds.IsFailure)
            return Result.Failure<IReadOnlyList<PokerRoundDto>>(rounds.Error);

        var timestamp = _dateTimeProvider.Now;
        foreach (var round in rounds.Value)
        {
            var outcome = await Publish(session, round, cancellationToken);
            session.RecordRoundPublication(round.Id, outcome, actor, timestamp);
        }

        await _planningDbContext.SaveChangesAsync(cancellationToken);

        List<PokerRoundDto> published = [];
        foreach (var round in rounds.Value)
        {
            var dto = round.Adapt<PokerRoundDto>();
            published.Add(dto);
            await _notifier.NotifyRoundPublicationChanged(session.Id, round.Id, dto.Publication!);
        }

        return published;
    }

    private async Task<Result> Publish(PokerSession session, PokerRound round, CancellationToken cancellationToken) =>
        (round.Subject!.Kind, session.Host?.Kind) switch
        {
            (PokerRoundSubjectKind.IntakeRequest, PokerSessionHostKind.IntakeDesk) => await _dispatcher.Send(
                new PublishPokerRoundToIntakeRequestCommand(round.Subject.Id, session.Host!.Id, session.EstimationScaleVersionId,
                    round.ConsensusEstimate!, session.Id, round.Id),
                cancellationToken),
            _ => Result.Failure(NoPublisherError),
        };
}
