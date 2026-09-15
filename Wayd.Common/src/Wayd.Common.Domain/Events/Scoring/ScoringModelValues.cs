namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A scoring model's editable details taken together, as <see cref="ScoringModelDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ScoringModelDetails(string Name, string Description);

/// <summary>
/// A criterion's name, token and description taken together, as
/// <see cref="ScoringModelCriterionDetailsUpdatedEvent.Previous"/> records the values an edit replaced.
/// </summary>
public sealed record ScoringCriterionDetails(string Name, string Token, string? Description);

/// <summary>
/// An output's name and token taken together, as <see cref="ScoringModelOutputDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record ScoringOutputDetails(string Name, string Token);

/// <summary>
/// One rating scale and its levels as a scoring model was created with them.
/// </summary>
/// <param name="Levels">The scale's levels, in display order.</param>
public sealed record ScoringScaleValues(Guid ScaleId, string Name, int Order, ScoringRatingLevelValues[] Levels);

/// <summary>
/// One rating level of a <see cref="ScoringScaleValues"/>.
/// </summary>
public sealed record ScoringRatingLevelValues(Guid LevelId, string Label, decimal Value, int Order);

/// <summary>
/// One criterion as a scoring model was created with it.
/// </summary>
/// <param name="ScaleId">The scale it is rated against, or null for free numeric entry.</param>
public sealed record ScoringCriterionValues(
    Guid CriterionId,
    string Name,
    string Token,
    string? Description,
    decimal? Weight,
    Guid? ScaleId,
    int Order);

/// <summary>
/// One output as a scoring model was created with it.
/// </summary>
public sealed record ScoringOutputValues(
    Guid OutputId,
    string Name,
    string Token,
    string Formula,
    bool IsPrimary,
    int Order);
