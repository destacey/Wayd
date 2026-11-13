namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// The planning poker round an intake request's estimate was published from.
/// </summary>
/// <param name="SessionId">The poker session the round belongs to.</param>
/// <param name="RoundId">The round whose consensus was published.</param>
public sealed record IntakeRequestEstimatePokerRound(Guid SessionId, Guid RoundId);
