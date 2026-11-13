namespace Wayd.Common.Application.Requests.DemandManagement;

/// <summary>
/// Writes the consensus of a planning poker round onto the intake request it estimated, as the caller. Sent by
/// Planning when a facilitator publishes the round.
/// </summary>
/// <remarks>
/// Demand Management decides again, at this moment, whether the estimate may be written: the caller must be an
/// agent on the request's desk, the request open, visible to them and still on the desk hosting the session, and
/// that desk must still pin the version the session voted on. A failure writes nothing, and its message is the
/// reason the round shows. A value that is a card of the version, such as <c>?</c>, clears the request's estimate
/// and is recorded on it in the estimate's place.
/// </remarks>
/// <param name="IntakeRequestId">The request the round estimated.</param>
/// <param name="IntakeDeskId">The desk hosting the round's session.</param>
/// <param name="EstimationScaleVersionId">The scale version the session voted on.</param>
/// <param name="Value">The round's consensus: an estimate value or a card of that version.</param>
/// <param name="PokerSessionId">The round's session.</param>
/// <param name="PokerRoundId">The round.</param>
public sealed record PublishPokerRoundToIntakeRequestCommand(
    Guid IntakeRequestId,
    Guid IntakeDeskId,
    Guid EstimationScaleVersionId,
    string Value,
    Guid PokerSessionId,
    Guid PokerRoundId) : ICommand, IRequireLinkedEmployee;
