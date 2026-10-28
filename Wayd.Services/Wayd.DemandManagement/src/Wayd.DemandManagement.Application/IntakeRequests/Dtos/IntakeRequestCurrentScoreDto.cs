using Wayd.Common.Application.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A request's current score, as the requests list and board rank on it. Substance tier, which is why it is
/// returned beside the summaries rather than on them.
/// </summary>
public sealed record IntakeRequestCurrentScoreDto
{
    /// <summary>The request scored.</summary>
    public Guid RequestId { get; init; }

    /// <summary>The value of the model's primary output.</summary>
    public decimal Value { get; init; }

    /// <summary>When the request was last scored.</summary>
    public Instant ScoredOn { get; init; }

    /// <summary>The scoring model the score came from, as it was named then.</summary>
    public required NavigationDto ScoringModel { get; init; }

    /// <summary>
    /// Whether the score came from the model the request's desk scores with now. A score from an earlier model,
    /// or from the desk a request moved from, does not rank with the rest.
    /// </summary>
    public bool IsDeskModel { get; init; }
}
