using Wayd.Common.Application.Scoring.ScoringModels.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// Everything a request's Score tab shows: the model its desk scores with, the score history, and where the
/// request ranks on its desk. Substance tier.
/// </summary>
public sealed record IntakeRequestScoringDto
{
    /// <summary>
    /// The model the request's desk scores with, with the criteria, scales and outputs a rating form needs; null
    /// when the desk does not score its requests.
    /// </summary>
    public ScoringModelDetailsDto? ScoringModel { get; init; }

    /// <summary>
    /// Whether the desk's model has been archived since it was assigned. It still scores, but an admin is
    /// expected to replace it.
    /// </summary>
    public bool ScoringModelArchived { get; init; }

    /// <summary>
    /// Whether the reader may score the request now: an agent on its desk, while the request is open and the
    /// desk has a model.
    /// </summary>
    public bool CanScore { get; init; }

    /// <summary>Every score the request has been given, newest first; the first is the current score.</summary>
    public required IReadOnlyList<IntakeRequestScoreDto> Scores { get; init; }

    /// <summary>
    /// Where the request ranks among the open requests on its desk scored with the desk's current model, or null
    /// when it is closed, has never been scored, or its current score came from another model.
    /// </summary>
    public IntakeRequestDeskRankDto? DeskRank { get; init; }
}

/// <summary>
/// A request's place among the open requests on its desk, by primary score.
/// </summary>
/// <remarks>
/// Counted only over requests the reader may read the score of, so it never discloses a score they cannot
/// see. Requests tied on the value share a position.
/// </remarks>
public sealed record IntakeRequestDeskRankDto
{
    /// <summary>The request's position, 1 being the highest score.</summary>
    public int Position { get; init; }

    /// <summary>How many open requests on the desk are ranked.</summary>
    public int Of { get; init; }
}
