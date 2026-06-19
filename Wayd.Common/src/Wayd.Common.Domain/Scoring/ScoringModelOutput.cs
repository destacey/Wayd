using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Represents a named, computed output of a scoring model — a formula over criterion tokens and
/// earlier output tokens (e.g. "CoD = BV + TC + RR", then "WSJF = CoD / JS"). Exactly one output per
/// model is the <see cref="IsPrimary"/> score; the others are intermediate values retained for display
/// and ranking (e.g. Cost of Delay).
/// </summary>
public sealed class ScoringModelOutput : BaseAuditableEntity
{
    private ScoringModelOutput() { }

    internal ScoringModelOutput(Guid scoringModelId, string name, string token, string formula, bool isPrimary, int order)
    {
        ScoringModelId = scoringModelId;
        Name = name;
        Token = token;
        Formula = formula;
        IsPrimary = isPrimary;
        Order = order;
    }

    /// <summary>
    /// The ID of the scoring model this output belongs to.
    /// </summary>
    public Guid ScoringModelId { get; private init; }

    /// <summary>
    /// The name of the output (e.g., "Cost of Delay", "WSJF").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The short identifier other output formulas can reference (e.g., "CoD", "WSJF").
    /// Unique within the model across criteria and outputs.
    /// </summary>
    public string Token
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Token)).Trim();
    } = default!;

    /// <summary>
    /// The arithmetic formula computing this output, referencing criterion tokens and the tokens of
    /// outputs ordered before it.
    /// </summary>
    public string Formula
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Formula)).Trim();
    } = default!;

    /// <summary>
    /// Whether this output is the model's primary score. Exactly one output per model is primary.
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>
    /// The evaluation/display order of the output within the scoring model. Outputs evaluate in this
    /// order so a later output may reference an earlier one.
    /// </summary>
    public int Order { get; internal set; }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    /// <summary>
    /// Updates the output details.
    /// </summary>
    internal Result Update(string name, string token, string formula, bool isPrimary)
    {
        Name = name;
        Token = token;
        Formula = formula;
        IsPrimary = isPrimary;
        return Result.Success();
    }
}
