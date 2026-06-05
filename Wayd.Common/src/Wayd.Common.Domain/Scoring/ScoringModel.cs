using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Scoring.Enums;

namespace Wayd.Common.Domain.Scoring;

/// <summary>
/// Represents a reusable scoring model defined by an administrator. A model is made up of rated
/// <see cref="Criteria"/> (the inputs) and a set of named <see cref="Outputs"/> (formulas over criterion
/// and earlier-output tokens). Exactly one output is the primary score; the rest are intermediate values
/// (e.g. Cost of Delay) retained for display and ranking. Consumers assign an active model to score and
/// rank items.
/// </summary>
/// <remarks>
/// Models follow a Proposed → Active → Archived lifecycle. Only Proposed models can be edited or
/// deleted; once Active, a model is locked so that scores produced from it stay stable. To change an
/// in-use model, archive it and create a new one.
/// </remarks>
public sealed class ScoringModel : BaseAuditableEntity, IHasIdAndKey
{
    private const string NotProposedError = "Only proposed scoring models can be modified.";

    private readonly List<ScoringModelCriterion> _criteria = [];
    private readonly List<ScoringScale> _scales = [];
    private readonly List<ScoringModelOutput> _outputs = [];

    private ScoringModel() { }

    private ScoringModel(string name, string description)
    {
        Name = name;
        Description = description;
        State = ScoringModelState.Proposed;
    }

    /// <summary>
    /// The unique auto-generated key of the scoring model. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the scoring model (e.g., "Weighted Criteria Matrix", "WSJF").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A description of the model's purpose and recommended use cases.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current state of the scoring model (Proposed, Active, Archived).
    /// </summary>
    public ScoringModelState State { get; private set; }

    /// <summary>
    /// The rated criteria (inputs) that make up this scoring model.
    /// </summary>
    public IReadOnlyCollection<ScoringModelCriterion> Criteria => _criteria.AsReadOnly();

    /// <summary>
    /// The named, reusable rating scales defined on this model. Criteria reference a scale to constrain
    /// how they are rated.
    /// </summary>
    public IReadOnlyCollection<ScoringScale> Scales => _scales.AsReadOnly();

    /// <summary>
    /// The named output formulas of this model, in evaluation order.
    /// </summary>
    public IReadOnlyCollection<ScoringModelOutput> Outputs => _outputs.AsReadOnly();

    /// <summary>
    /// Indicates whether the model can be deleted. Only proposed models can be deleted.
    /// </summary>
    public bool CanBeDeleted() => State is ScoringModelState.Proposed;

    /// <summary>
    /// Updates the model details. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result Update(string name, string description)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure(NotProposedError);
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    #region State Transitions

    /// <summary>
    /// Activates the model, making it available for assignment. Requires at least one criterion, every
    /// scale-referencing criterion to point at a scale with at least two levels, and a valid set of
    /// outputs with exactly one primary, where each output formula references only tokens defined before it.
    /// </summary>
    public Result Activate()
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Only proposed scoring models can be activated.");
        }

        if (_criteria.Count < 1)
        {
            return Result.Failure("A scoring model must have at least one criterion before it can be activated.");
        }

        var scalesValidation = ValidateReferencedScales();
        if (scalesValidation.IsFailure)
        {
            return scalesValidation;
        }

        var outputsValidation = ValidateOutputs();
        if (outputsValidation.IsFailure)
        {
            return outputsValidation;
        }

        State = ScoringModelState.Active;

        return Result.Success();
    }

    /// <summary>
    /// Archives the model, preventing it from being assigned. Existing scores and assignments are not affected.
    /// </summary>
    public Result Archive()
    {
        if (State != ScoringModelState.Active)
        {
            return Result.Failure("Only active scoring models can be archived.");
        }

        State = ScoringModelState.Archived;

        return Result.Success();
    }

    #endregion State Transitions

    #region Criteria Management

    /// <summary>
    /// Adds a new criterion to the model. Only allowed when the model is in the Proposed state.
    /// The criterion is appended at the end of the existing criteria.
    /// </summary>
    public Result<ScoringModelCriterion> AddCriterion(string name, string token, string? description, decimal? weight, Guid? scaleId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure<ScoringModelCriterion>("Criteria can only be added to proposed scoring models.");
        }

        var tokenResult = ValidateNewToken(token);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<ScoringModelCriterion>(tokenResult.Error);
        }

        var scaleResult = ValidateScaleReference(scaleId);
        if (scaleResult.IsFailure)
        {
            return Result.Failure<ScoringModelCriterion>(scaleResult.Error);
        }

        var order = _criteria.Count > 0 ? _criteria.Max(c => c.Order) + 1 : 1;

        var criterion = new ScoringModelCriterion(Id, name, token.Trim(), description, weight, scaleId, order);
        _criteria.Add(criterion);

        return Result.Success(criterion);
    }

    /// <summary>
    /// Updates the details of an existing criterion. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateCriterion(Guid criterionId, string name, string token, string? description, decimal? weight, Guid? scaleId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Criteria can only be updated on proposed scoring models.");
        }

        var criterion = _criteria.FirstOrDefault(c => c.Id == criterionId);
        if (criterion is null)
        {
            return Result.Failure("Criterion not found.");
        }

        var tokenResult = ValidateNewToken(token, exceptCriterionId: criterionId);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        var scaleResult = ValidateScaleReference(scaleId);
        if (scaleResult.IsFailure)
        {
            return scaleResult;
        }

        return criterion.Update(name, token.Trim(), description, weight, scaleId);
    }

    /// <summary>
    /// Removes a criterion from the model and reorders the remaining criteria.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result RemoveCriterion(Guid criterionId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Criteria can only be removed from proposed scoring models.");
        }

        var criterion = _criteria.FirstOrDefault(c => c.Id == criterionId);
        if (criterion is null)
        {
            return Result.Failure("Criterion not found.");
        }

        _criteria.Remove(criterion);

        ReorderCriteria();

        return Result.Success();
    }

    /// <summary>
    /// Reorders the criteria based on the provided ordered list of criterion IDs.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderCriteria(List<Guid> orderedCriterionIds)
    {
        Guard.Against.Null(orderedCriterionIds, nameof(orderedCriterionIds));

        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Criteria can only be reordered on proposed scoring models.");
        }

        if (orderedCriterionIds.Count != _criteria.Count)
        {
            return Result.Failure("The number of criterion IDs must match the number of existing criteria.");
        }

        if (orderedCriterionIds.Distinct().Count() != orderedCriterionIds.Count)
        {
            return Result.Failure("Duplicate criterion IDs are not allowed.");
        }

        for (int i = 0; i < orderedCriterionIds.Count; i++)
        {
            var criterion = _criteria.FirstOrDefault(c => c.Id == orderedCriterionIds[i]);
            if (criterion is null)
            {
                return Result.Failure($"Criterion with ID '{orderedCriterionIds[i]}' not found.");
            }

            criterion.Order = i + 1;
        }

        return Result.Success();
    }

    /// <summary>
    /// Resets criteria ordering to eliminate gaps after removal.
    /// </summary>
    private void ReorderCriteria()
    {
        int order = 1;
        foreach (var criterion in _criteria.OrderBy(c => c.Order))
        {
            criterion.Order = order;
            order++;
        }
    }

    #endregion Criteria Management

    #region Scale Management

    /// <summary>
    /// Adds a new named rating scale to the model. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result<ScoringScale> AddScale(string name)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure<ScoringScale>("Scales can only be added to proposed scoring models.");
        }

        var nameResult = ValidateNewScaleName(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<ScoringScale>(nameResult.Error);
        }

        var order = _scales.Count > 0 ? _scales.Max(s => s.Order) + 1 : 1;

        var scale = new ScoringScale(Id, name.Trim(), order);
        _scales.Add(scale);

        return Result.Success(scale);
    }

    /// <summary>
    /// Renames an existing scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateScale(Guid scaleId, string name)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Scales can only be updated on proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure("Scale not found.");
        }

        var nameResult = ValidateNewScaleName(name, exceptScaleId: scaleId);
        if (nameResult.IsFailure)
        {
            return nameResult;
        }

        return scale.Update(name.Trim());
    }

    /// <summary>
    /// Removes a scale and reorders the remaining scales. Only allowed when the model is in the Proposed
    /// state, and only when no criterion references the scale.
    /// </summary>
    public Result RemoveScale(Guid scaleId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Scales can only be removed from proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure("Scale not found.");
        }

        if (_criteria.Any(c => c.ScaleId == scaleId))
        {
            return Result.Failure("This scale is referenced by one or more criteria. Reassign those criteria before removing the scale.");
        }

        _scales.Remove(scale);

        ReorderScales();

        return Result.Success();
    }

    /// <summary>
    /// Reorders the scales based on the provided ordered list of scale IDs.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderScales(List<Guid> orderedScaleIds)
    {
        Guard.Against.Null(orderedScaleIds, nameof(orderedScaleIds));

        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Scales can only be reordered on proposed scoring models.");
        }

        if (orderedScaleIds.Count != _scales.Count)
        {
            return Result.Failure("The number of scale IDs must match the number of existing scales.");
        }

        if (orderedScaleIds.Distinct().Count() != orderedScaleIds.Count)
        {
            return Result.Failure("Duplicate scale IDs are not allowed.");
        }

        for (int i = 0; i < orderedScaleIds.Count; i++)
        {
            var scale = _scales.FirstOrDefault(s => s.Id == orderedScaleIds[i]);
            if (scale is null)
            {
                return Result.Failure($"Scale with ID '{orderedScaleIds[i]}' not found.");
            }

            scale.Order = i + 1;
        }

        return Result.Success();
    }

    private void ReorderScales()
    {
        int order = 1;
        foreach (var scale in _scales.OrderBy(s => s.Order))
        {
            scale.Order = order;
            order++;
        }
    }

    #endregion Scale Management

    #region Scale Level Management

    /// <summary>
    /// Adds a rating level to a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result<ScoringRatingLevel> AddScaleLevel(Guid scaleId, string label, decimal value)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure<ScoringRatingLevel>("Rating levels can only be added to proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure<ScoringRatingLevel>("Scale not found.");
        }

        return Result.Success(scale.AddLevel(label, value));
    }

    /// <summary>
    /// Updates a rating level on a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateScaleLevel(Guid scaleId, Guid levelId, string label, decimal value)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Rating levels can only be updated on proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure("Scale not found.");
        }

        return scale.UpdateLevel(levelId, label, value);
    }

    /// <summary>
    /// Removes a rating level from a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result RemoveScaleLevel(Guid scaleId, Guid levelId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Rating levels can only be removed from proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure("Scale not found.");
        }

        return scale.RemoveLevel(levelId);
    }

    /// <summary>
    /// Reorders the rating levels within a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderScaleLevels(Guid scaleId, List<Guid> orderedLevelIds)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Rating levels can only be reordered on proposed scoring models.");
        }

        var scale = _scales.FirstOrDefault(s => s.Id == scaleId);
        if (scale is null)
        {
            return Result.Failure("Scale not found.");
        }

        return scale.ReorderLevels(orderedLevelIds);
    }

    #endregion Scale Level Management

    #region Output Management

    /// <summary>
    /// Adds a new output formula to the model. Only allowed when the model is in the Proposed state.
    /// The formula may reference criterion tokens and the tokens of outputs already defined. If this is
    /// the first output, it becomes primary by default; marking it primary transfers the flag.
    /// </summary>
    public Result<ScoringModelOutput> AddOutput(string name, string token, string formula, bool isPrimary)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure<ScoringModelOutput>("Outputs can only be added to proposed scoring models.");
        }

        var tokenResult = ValidateNewToken(token);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<ScoringModelOutput>(tokenResult.Error);
        }

        var order = _outputs.Count > 0 ? _outputs.Max(o => o.Order) + 1 : 1;

        // The formula may reference criteria and outputs ordered before this one.
        var allowedTokens = TokensAvailableBefore(order);
        var formulaResult = ScoringFormulaEvaluator.Validate(formula, allowedTokens);
        if (formulaResult.IsFailure)
        {
            return Result.Failure<ScoringModelOutput>(formulaResult.Error);
        }

        var makePrimary = isPrimary || _outputs.Count == 0;

        var output = new ScoringModelOutput(Id, name, token.Trim(), formula.Trim(), makePrimary, order);
        _outputs.Add(output);

        if (makePrimary)
        {
            DemoteOtherPrimaries(output);
        }

        return Result.Success(output);
    }

    /// <summary>
    /// Updates an existing output. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateOutput(Guid outputId, string name, string token, string formula, bool isPrimary)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Outputs can only be updated on proposed scoring models.");
        }

        var output = _outputs.FirstOrDefault(o => o.Id == outputId);
        if (output is null)
        {
            return Result.Failure("Output not found.");
        }

        var tokenResult = ValidateNewToken(token, exceptOutputId: outputId);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        var allowedTokens = TokensAvailableBefore(output.Order, exceptOutputId: outputId);
        var formulaResult = ScoringFormulaEvaluator.Validate(formula, allowedTokens);
        if (formulaResult.IsFailure)
        {
            return formulaResult;
        }

        var makePrimary = isPrimary || (output.IsPrimary && _outputs.Count == 1);

        var updateResult = output.Update(name, token.Trim(), formula.Trim(), makePrimary);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        if (makePrimary)
        {
            DemoteOtherPrimaries(output);
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes an output and reorders the remaining outputs. Only allowed when the model is in the
    /// Proposed state. If the primary output is removed, the first remaining output becomes primary.
    /// </summary>
    public Result RemoveOutput(Guid outputId)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Outputs can only be removed from proposed scoring models.");
        }

        var output = _outputs.FirstOrDefault(o => o.Id == outputId);
        if (output is null)
        {
            return Result.Failure("Output not found.");
        }

        var wasPrimary = output.IsPrimary;

        _outputs.Remove(output);

        ReorderOutputsInternal();

        if (wasPrimary)
        {
            var first = _outputs.OrderBy(o => o.Order).FirstOrDefault();
            first?.SetPrimary(true);
        }

        return Result.Success();
    }

    /// <summary>
    /// Reorders the outputs based on the provided ordered list of output IDs. Only allowed when the model
    /// is in the Proposed state. The new order must keep each output's formula referencing only tokens
    /// that precede it.
    /// </summary>
    public Result ReorderOutputs(List<Guid> orderedOutputIds)
    {
        Guard.Against.Null(orderedOutputIds, nameof(orderedOutputIds));

        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure("Outputs can only be reordered on proposed scoring models.");
        }

        if (orderedOutputIds.Count != _outputs.Count)
        {
            return Result.Failure("The number of output IDs must match the number of existing outputs.");
        }

        if (orderedOutputIds.Distinct().Count() != orderedOutputIds.Count)
        {
            return Result.Failure("Duplicate output IDs are not allowed.");
        }

        // Validate the proposed ordering keeps every formula referencing only earlier tokens.
        var criterionTokens = _criteria.Select(c => c.Token).ToHashSet(StringComparer.Ordinal);
        var availableSoFar = new HashSet<string>(criterionTokens, StringComparer.Ordinal);
        foreach (var outputId in orderedOutputIds)
        {
            var output = _outputs.FirstOrDefault(o => o.Id == outputId);
            if (output is null)
            {
                return Result.Failure($"Output with ID '{outputId}' not found.");
            }

            var formulaResult = ScoringFormulaEvaluator.Validate(output.Formula, availableSoFar);
            if (formulaResult.IsFailure)
            {
                return Result.Failure($"Output '{output.Name}' would reference a token not yet defined in this order: {formulaResult.Error}");
            }

            availableSoFar.Add(output.Token);
        }

        for (int i = 0; i < orderedOutputIds.Count; i++)
        {
            _outputs.First(o => o.Id == orderedOutputIds[i]).Order = i + 1;
        }

        return Result.Success();
    }

    private void ReorderOutputsInternal()
    {
        int order = 1;
        foreach (var output in _outputs.OrderBy(o => o.Order))
        {
            output.Order = order;
            order++;
        }
    }

    private void DemoteOtherPrimaries(ScoringModelOutput primary)
    {
        foreach (var other in _outputs.Where(o => o.Id != primary.Id && o.IsPrimary))
        {
            other.SetPrimary(false);
        }
    }

    /// <summary>
    /// The criterion tokens plus the tokens of outputs ordered before <paramref name="order"/>.
    /// Used to constrain a formula to referencing only previously-defined tokens.
    /// </summary>
    private HashSet<string> TokensAvailableBefore(int order, Guid? exceptOutputId = null)
    {
        var tokens = _criteria.Select(c => c.Token).ToHashSet(StringComparer.Ordinal);
        foreach (var output in _outputs.Where(o => o.Order < order && o.Id != exceptOutputId))
        {
            tokens.Add(output.Token);
        }
        return tokens;
    }

    #endregion Output Management

    #region Scoring

    /// <summary>
    /// Calculates the model's outputs for a set of selected rating values keyed by criterion ID. Outputs
    /// are evaluated in order — each may use criterion values and prior output values — and the result
    /// carries every output value plus the primary score. Returns a failure if any criterion is unrated,
    /// the model is not evaluable, or a formula fails (e.g. division by zero).
    /// </summary>
    /// <param name="ratingValuesByCriterionId">
    /// The selected rating <see cref="ScoringRatingLevel.Value"/> for each criterion, keyed by criterion ID.
    /// </param>
    public Result<ScoringResult> CalculateScore(IReadOnlyDictionary<Guid, decimal> ratingValuesByCriterionId)
    {
        Guard.Against.Null(ratingValuesByCriterionId, nameof(ratingValuesByCriterionId));

        if (_criteria.Count == 0)
        {
            return Result.Failure<ScoringResult>("The scoring model has no criteria to score.");
        }

        if (_outputs.Count == 0)
        {
            return Result.Failure<ScoringResult>("The scoring model has no outputs to calculate.");
        }

        var primary = _outputs.SingleOrDefault(o => o.IsPrimary);
        if (primary is null)
        {
            return Result.Failure<ScoringResult>("The scoring model must have exactly one primary output.");
        }

        // Seed the token map with each criterion's rated value.
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var criterion in _criteria)
        {
            if (!ratingValuesByCriterionId.TryGetValue(criterion.Id, out var value))
            {
                return Result.Failure<ScoringResult>($"Criterion '{criterion.Name}' has not been rated.");
            }

            values[criterion.Token] = value;
        }

        // Evaluate outputs in order, making each available to later formulas.
        var outputValues = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var output in _outputs.OrderBy(o => o.Order))
        {
            var evaluation = ScoringFormulaEvaluator.Evaluate(output.Formula, values);
            if (evaluation.IsFailure)
            {
                return Result.Failure<ScoringResult>($"Output '{output.Name}' could not be calculated: {evaluation.Error}");
            }

            values[output.Token] = evaluation.Value;
            outputValues[output.Token] = evaluation.Value;
        }

        return Result.Success(new ScoringResult(outputValues[primary.Token], outputValues));
    }

    #endregion Scoring

    #region Validation Helpers

    private Result ValidateNewToken(string token, Guid? exceptCriterionId = null, Guid? exceptOutputId = null)
    {
        var formatResult = ScoringToken.Validate(token);
        if (formatResult.IsFailure)
        {
            return formatResult;
        }

        var trimmed = token.Trim();

        var collides =
            _criteria.Any(c => c.Id != exceptCriterionId && string.Equals(c.Token, trimmed, StringComparison.Ordinal))
            || _outputs.Any(o => o.Id != exceptOutputId && string.Equals(o.Token, trimmed, StringComparison.Ordinal));

        return collides
            ? Result.Failure($"Token '{trimmed}' is already used by another criterion or output.")
            : Result.Success();
    }

    private Result ValidateNewScaleName(string name, Guid? exceptScaleId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure("Scale name must not be empty.");
        }

        var trimmed = name.Trim();

        return _scales.Any(s => s.Id != exceptScaleId && string.Equals(s.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            ? Result.Failure($"A scale named '{trimmed}' already exists.")
            : Result.Success();
    }

    private Result ValidateScaleReference(Guid? scaleId)
    {
        if (scaleId is null)
        {
            return Result.Success();
        }

        return _scales.Any(s => s.Id == scaleId.Value)
            ? Result.Success()
            : Result.Failure("The specified scale does not belong to this model.");
    }

    private Result ValidateReferencedScales()
    {
        foreach (var criterion in _criteria.Where(c => c.ScaleId is not null))
        {
            var scale = _scales.FirstOrDefault(s => s.Id == criterion.ScaleId!.Value);
            if (scale is null)
            {
                return Result.Failure($"Criterion '{criterion.Name}' references a scale that no longer exists.");
            }

            if (scale.Levels.Count < 2)
            {
                return Result.Failure($"Scale '{scale.Name}' (used by criterion '{criterion.Name}') must have at least two rating levels.");
            }
        }

        return Result.Success();
    }

    private Result ValidateOutputs()
    {
        if (_outputs.Count == 0)
        {
            return Result.Failure("A scoring model must have at least one output before it can be activated.");
        }

        if (_outputs.Count(o => o.IsPrimary) != 1)
        {
            return Result.Failure("A scoring model must have exactly one primary output.");
        }

        var availableSoFar = _criteria.Select(c => c.Token).ToHashSet(StringComparer.Ordinal);
        foreach (var output in _outputs.OrderBy(o => o.Order))
        {
            var formulaResult = ScoringFormulaEvaluator.Validate(output.Formula, availableSoFar);
            if (formulaResult.IsFailure)
            {
                return Result.Failure($"Output '{output.Name}' has an invalid formula: {formulaResult.Error}");
            }

            availableSoFar.Add(output.Token);
        }

        return Result.Success();
    }

    #endregion Validation Helpers

    /// <summary>
    /// Creates a new scoring model in the Proposed state, optionally with initial scales, criteria, and
    /// outputs. Criteria reference a scale by name (must match one of the supplied scales) or omit it for
    /// free numeric entry.
    /// </summary>
    /// <param name="name">The name of the scoring model.</param>
    /// <param name="description">A description of the model's purpose and use cases.</param>
    /// <param name="scales">Optional initial scales, each a name plus its ordered (label, value) levels.</param>
    /// <param name="criteria">Optional initial criteria, each as (name, token, description, weight, scaleName?).</param>
    /// <param name="outputs">Optional initial outputs, each as (name, token, formula, isPrimary).</param>
    public static ScoringModel Create(
        string name,
        string description,
        IEnumerable<(string Name, IEnumerable<(string Label, decimal Value)> Levels)>? scales = null,
        IEnumerable<(string Name, string Token, string? Description, decimal? Weight, string? ScaleName)>? criteria = null,
        IEnumerable<(string Name, string Token, string Formula, bool IsPrimary)>? outputs = null)
    {
        var model = new ScoringModel(name, description);

        var scalesByName = new Dictionary<string, ScoringScale>(StringComparer.OrdinalIgnoreCase);
        if (scales is not null)
        {
            int scaleOrder = 1;
            foreach (var (scaleName, levels) in scales)
            {
                var scale = new ScoringScale(model.Id, scaleName, scaleOrder);
                int levelOrder = 1;
                foreach (var (label, value) in levels)
                {
                    scale.SeedLevel(label, value, levelOrder);
                    levelOrder++;
                }
                model._scales.Add(scale);
                scalesByName[scale.Name] = scale;
                scaleOrder++;
            }
        }

        if (criteria is not null)
        {
            int order = 1;
            foreach (var (criterionName, token, criterionDescription, weight, scaleName) in criteria)
            {
                Guid? scaleId = scaleName is not null && scalesByName.TryGetValue(scaleName, out var scale)
                    ? scale.Id
                    : null;
                model._criteria.Add(new ScoringModelCriterion(model.Id, criterionName, token, criterionDescription, weight, scaleId, order));
                order++;
            }
        }

        if (outputs is not null)
        {
            int order = 1;
            foreach (var (outputName, token, formula, isPrimary) in outputs)
            {
                model._outputs.Add(new ScoringModelOutput(model.Id, outputName, token, formula, isPrimary, order));
                order++;
            }

            // If no output was flagged primary, default the first to primary.
            if (model._outputs.Count > 0 && !model._outputs.Any(o => o.IsPrimary))
            {
                model._outputs.OrderBy(o => o.Order).First().SetPrimary(true);
            }
        }

        return model;
    }
}
