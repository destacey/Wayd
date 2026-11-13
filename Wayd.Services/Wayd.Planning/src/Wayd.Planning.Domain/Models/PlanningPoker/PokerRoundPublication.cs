using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Domain.Models.PlanningPoker;

/// <summary>
/// The last attempt to publish a poker round's consensus to the record it estimates: taken, or refused with the
/// reason the record's area gave.
/// </summary>
/// <remarks>
/// A round with none has never been published since it was last accepted. Only a published round wrote anything;
/// a refused one can be published again.
/// </remarks>
public sealed record PokerRoundPublication
{
    /// <summary>The longest refusal reason kept. A longer one keeps its start.</summary>
    public const int RefusalReasonMaxLength = 512;

    private PokerRoundPublication() { }

    private PokerRoundPublication(PokerRoundPublicationOutcome outcome, Instant at, Guid byEmployeeId, string? refusalReason)
    {
        Outcome = outcome;
        At = at;
        ByEmployeeId = Guard.Against.Default(byEmployeeId, nameof(byEmployeeId));
        RefusalReason = refusalReason;
    }

    /// <summary>Whether the consensus was taken or refused.</summary>
    public PokerRoundPublicationOutcome Outcome { get; private init; }

    /// <summary>When the facilitator published, or tried to.</summary>
    public Instant At { get; private init; }

    /// <summary>The facilitator who published, or tried to.</summary>
    public Guid ByEmployeeId { get; private init; }

    /// <summary>Why the record's area refused the consensus, or null when it was taken.</summary>
    public string? RefusalReason { get; private init; }

    /// <summary>The consensus was written to the record.</summary>
    public static PokerRoundPublication Published(Guid byEmployeeId, Instant at) =>
        new(PokerRoundPublicationOutcome.Published, at, byEmployeeId, null);

    /// <summary>The record's area refused the consensus, for the reason it gave.</summary>
    public static PokerRoundPublication Refused(string reason, Guid byEmployeeId, Instant at)
    {
        var trimmed = Guard.Against.NullOrWhiteSpace(reason, nameof(reason)).Trim();
        return new(PokerRoundPublicationOutcome.Refused, at, byEmployeeId,
            trimmed.Length <= RefusalReasonMaxLength ? trimmed : trimmed[..RefusalReasonMaxLength].TrimEnd());
    }
}
