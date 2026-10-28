using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One of an intake request's scores: the frozen ratings and outputs it was given at a point in time. Substance
/// tier.
/// </summary>
public sealed record IntakeRequestScoreDto
{
    /// <summary>The score's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The request's score number; the highest is the current score.</summary>
    public long Sequence { get; init; }

    /// <summary>The value of the model's primary output, which requests are ranked on.</summary>
    public decimal PrimaryValue { get; init; }

    /// <summary>When the score was recorded.</summary>
    public Instant ScoredOn { get; init; }

    /// <summary>The agent who rated the request.</summary>
    public required EmployeeNavigationDto ScoredBy { get; init; }

    /// <summary>The scoring model the request was rated with, as it was named then.</summary>
    public required NavigationDto ScoringModel { get; init; }

    /// <summary>Each criterion's rating, in the model's display order.</summary>
    public required IReadOnlyList<IntakeRequestScoreRatingDto> Ratings { get; init; }

    /// <summary>Every computed output, in evaluation order.</summary>
    public required IReadOnlyList<IntakeRequestScoreOutputDto> Outputs { get; init; }
}

/// <summary>
/// One criterion's frozen rating within an intake request's score.
/// </summary>
public sealed record IntakeRequestScoreRatingDto
{
    /// <summary>The criterion rated.</summary>
    public Guid CriterionId { get; init; }

    /// <summary>The criterion's name when the request was scored.</summary>
    public required string CriterionName { get; init; }

    /// <summary>The criterion's formula token when the request was scored.</summary>
    public required string CriterionToken { get; init; }

    /// <summary>The number the calculation used.</summary>
    public decimal RatingValue { get; init; }

    /// <summary>The level chosen, when the criterion is rated on a scale.</summary>
    public Guid? RatingLevelId { get; init; }

    /// <summary>The chosen level's label when the request was scored; null for a number entered directly.</summary>
    public string? RatingLevelLabel { get; init; }

    /// <summary>The criterion's display order.</summary>
    public int Order { get; init; }
}

/// <summary>
/// One frozen computed output within an intake request's score.
/// </summary>
public sealed record IntakeRequestScoreOutputDto
{
    /// <summary>The output's formula token.</summary>
    public required string Token { get; init; }

    /// <summary>The output's name when the request was scored.</summary>
    public required string Name { get; init; }

    /// <summary>The computed value.</summary>
    public decimal Value { get; init; }

    /// <summary>Whether this was the model's primary output.</summary>
    public bool IsPrimary { get; init; }

    /// <summary>The output's evaluation order.</summary>
    public int Order { get; init; }
}
