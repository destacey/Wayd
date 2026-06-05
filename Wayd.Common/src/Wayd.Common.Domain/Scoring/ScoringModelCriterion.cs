using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using Wayd.Common.Extensions;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Represents a single rated input within a scoring model (e.g., "Business Value"). Each criterion is
/// rated against the model's shared rating scale, and exposes a <see cref="Token"/> that the model's
/// output formulas reference. An optional <see cref="Weight"/> is retained for the weighted-formula
/// scaffolding convenience but is not authoritative — the output formulas determine the score.
/// </summary>
public sealed class ScoringModelCriterion : BaseAuditableEntity
{
    private ScoringModelCriterion() { }

    internal ScoringModelCriterion(Guid scoringModelId, string name, string token, string? description, decimal? weight, Guid? scaleId, int order)
    {
        ScoringModelId = scoringModelId;
        Name = name;
        Token = token;
        Description = description;
        Weight = weight;
        ScaleId = scaleId;
        Order = order;
    }

    /// <summary>
    /// The ID of the scoring model this criterion belongs to.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The name of the criterion (e.g., "Business Value", "Job Size").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The short identifier referenced by the model's output formulas (e.g., "BV", "JS").
    /// Unique within the model across criteria and outputs.
    /// </summary>
    public string Token
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Token)).Trim();
    } = default!;

    /// <summary>
    /// An optional description clarifying what the criterion measures.
    /// </summary>
    public string? Description { get; private set => field = value.NullIfWhiteSpacePlusTrim(); }

    /// <summary>
    /// An optional, non-authoritative weight (used only by the weighted-formula scaffolder).
    /// The output formulas, not weights, determine the score.
    /// </summary>
    public decimal? Weight { get; private set; }

    /// <summary>
    /// The optional rating scale this criterion is rated against. When set, scorers must pick a level
    /// from the scale; when null, the criterion is rated by free numeric entry.
    /// </summary>
    public Guid? ScaleId { get; private set; }

    /// <summary>
    /// The display order of the criterion within the scoring model.
    /// </summary>
    public int Order { get; internal set; }

    /// <summary>
    /// Updates the criterion details.
    /// </summary>
    internal Result Update(string name, string token, string? description, decimal? weight, Guid? scaleId)
    {
        Name = name;
        Token = token;
        Description = description;
        Weight = weight;
        ScaleId = scaleId;
        return Result.Success();
    }
}
