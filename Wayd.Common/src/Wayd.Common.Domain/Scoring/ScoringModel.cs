using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Scoring;
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
    public Result Update(string name, string description, EventActor actor, Instant timestamp)
    {
        if (State != ScoringModelState.Proposed)
        {
            return Result.Failure(NotProposedError);
        }

        // Compared after assignment, never against the arguments: the setters trim.
        var before = new ScoringModelDetails(Name, Description);

        Name = name;
        Description = description;

        var after = new ScoringModelDetails(Name, Description);
        if (before != after)
        {
            AddKeyedDomainEvent(() => new ScoringModelDetailsUpdatedEvent(Id, Key, after.Name, after.Description, before, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Raises the deletion event, refusing a model that is not proposed. The caller removes the model in the
    /// same save, which is what drains the event.
    /// </summary>
    public Result Delete(EventActor actor, Instant timestamp)
    {
        if (!CanBeDeleted())
        {
            return Result.Failure("Only proposed scoring models can be deleted.");
        }

        AddDomainEvent(new ScoringModelDeletedEvent(Id, Key, Name, actor, timestamp));

        return Result.Success();
    }

    #region State Transitions

    /// <summary>
    /// Activates the model, making it available for assignment. Requires at least one criterion, every
    /// scale-referencing criterion to point at a scale with at least two levels, and a valid set of
    /// outputs with exactly one primary, where each output formula references only tokens defined before it.
    /// </summary>
    public Result Activate(EventActor actor, Instant timestamp)
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

        AddKeyedDomainEvent(() => new ScoringModelActivatedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Archives the model, preventing it from being assigned. Existing scores and assignments are not affected.
    /// </summary>
    public Result Archive(EventActor actor, Instant timestamp)
    {
        if (State != ScoringModelState.Active)
        {
            return Result.Failure("Only active scoring models can be archived.");
        }

        State = ScoringModelState.Archived;

        AddKeyedDomainEvent(() => new ScoringModelArchivedEvent(Id, Key, actor, timestamp));

        return Result.Success();
    }

    #endregion State Transitions

    #region Criteria Management

    /// <summary>
    /// Adds a new criterion to the model. Only allowed when the model is in the Proposed state.
    /// The criterion is appended at the end of the existing criteria.
    /// </summary>
    public Result<ScoringModelCriterion> AddCriterion(string name, string token, string? description, decimal? weight, Guid? scaleId, EventActor actor, Instant timestamp)
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

        var values = CriterionValues(criterion);
        AddKeyedDomainEvent(() => new ScoringModelCriterionAddedEvent(
            Id, Key, values.CriterionId, values.Name, values.Token, values.Description, values.Weight, values.ScaleId, values.Order,
            actor, timestamp));

        return Result.Success(criterion);
    }

    /// <summary>
    /// Updates the details of an existing criterion. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateCriterion(Guid criterionId, string name, string token, string? description, decimal? weight, Guid? scaleId, EventActor actor, Instant timestamp)
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

        var beforeDetails = new ScoringCriterionDetails(criterion.Name, criterion.Token, criterion.Description);
        var (beforeWeight, beforeScaleId) = (criterion.Weight, criterion.ScaleId);

        var updateResult = criterion.Update(name, token.Trim(), description, weight, scaleId);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var afterDetails = new ScoringCriterionDetails(criterion.Name, criterion.Token, criterion.Description);
        if (beforeDetails != afterDetails)
        {
            AddKeyedDomainEvent(() => new ScoringModelCriterionDetailsUpdatedEvent(
                Id, Key, criterionId, afterDetails.Name, afterDetails.Token, afterDetails.Description, beforeDetails, actor, timestamp));
        }

        var afterWeight = criterion.Weight;
        if (beforeWeight != afterWeight)
        {
            AddKeyedDomainEvent(() => new ScoringModelCriterionWeightChangedEvent(Id, Key, criterionId, beforeWeight, afterWeight, actor, timestamp));
        }

        var afterScaleId = criterion.ScaleId;
        if (beforeScaleId != afterScaleId)
        {
            AddKeyedDomainEvent(() => new ScoringModelCriterionScaleChangedEvent(Id, Key, criterionId, beforeScaleId, afterScaleId, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes a criterion from the model and reorders the remaining criteria.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result RemoveCriterion(Guid criterionId, EventActor actor, Instant timestamp)
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

        var (criterionName, criterionToken) = (criterion.Name, criterion.Token);
        AddKeyedDomainEvent(() => new ScoringModelCriterionRemovedEvent(Id, Key, criterionId, criterionName, criterionToken, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Reorders the criteria based on the provided ordered list of criterion IDs.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderCriteria(List<Guid> orderedCriterionIds, EventActor actor, Instant timestamp)
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

        var previousOrder = CriterionOrder();

        for (int i = 0; i < orderedCriterionIds.Count; i++)
        {
            var criterion = _criteria.FirstOrDefault(c => c.Id == orderedCriterionIds[i]);
            if (criterion is null)
            {
                return Result.Failure($"Criterion with ID '{orderedCriterionIds[i]}' not found.");
            }

            criterion.Order = i + 1;
        }

        var order = CriterionOrder();
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new ScoringModelCriteriaReorderedEvent(Id, Key, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    private Guid[] CriterionOrder() => [.. _criteria.OrderBy(c => c.Order).Select(c => c.Id)];

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
    public Result<ScoringScale> AddScale(string name, EventActor actor, Instant timestamp)
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

        var (scaleId, scaleName) = (scale.Id, scale.Name);
        AddKeyedDomainEvent(() => new ScoringModelScaleAddedEvent(Id, Key, scaleId, scaleName, order, actor, timestamp));

        return Result.Success(scale);
    }

    /// <summary>
    /// Renames an existing scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateScale(Guid scaleId, string name, EventActor actor, Instant timestamp)
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

        var previousName = scale.Name;

        var updateResult = scale.Update(name.Trim());
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var newName = scale.Name;
        if (previousName != newName)
        {
            AddKeyedDomainEvent(() => new ScoringModelScaleRenamedEvent(Id, Key, scaleId, previousName, newName, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes a scale and reorders the remaining scales. Only allowed when the model is in the Proposed
    /// state, and only when no criterion references the scale.
    /// </summary>
    public Result RemoveScale(Guid scaleId, EventActor actor, Instant timestamp)
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

        var scaleName = scale.Name;
        AddKeyedDomainEvent(() => new ScoringModelScaleRemovedEvent(Id, Key, scaleId, scaleName, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Reorders the scales based on the provided ordered list of scale IDs.
    /// Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderScales(List<Guid> orderedScaleIds, EventActor actor, Instant timestamp)
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

        var previousOrder = ScaleOrder();

        for (int i = 0; i < orderedScaleIds.Count; i++)
        {
            var scale = _scales.FirstOrDefault(s => s.Id == orderedScaleIds[i]);
            if (scale is null)
            {
                return Result.Failure($"Scale with ID '{orderedScaleIds[i]}' not found.");
            }

            scale.Order = i + 1;
        }

        var order = ScaleOrder();
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new ScoringModelScalesReorderedEvent(Id, Key, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    private Guid[] ScaleOrder() => [.. _scales.OrderBy(s => s.Order).Select(s => s.Id)];

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
    public Result<ScoringRatingLevel> AddScaleLevel(Guid scaleId, string label, decimal value, EventActor actor, Instant timestamp)
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

        var level = scale.AddLevel(label, value);

        var (levelId, levelLabel, levelValue, levelOrder) = (level.Id, level.Label, level.Value, level.Order);
        AddKeyedDomainEvent(() => new ScoringModelScaleLevelAddedEvent(
            Id, Key, scaleId, levelId, levelLabel, levelValue, levelOrder, actor, timestamp));

        return Result.Success(level);
    }

    /// <summary>
    /// Updates a rating level on a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateScaleLevel(Guid scaleId, Guid levelId, string label, decimal value, EventActor actor, Instant timestamp)
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

        var level = scale.Levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null)
        {
            return Result.Failure("Rating level not found.");
        }

        var (previousLabel, previousValue) = (level.Label, level.Value);

        var updateResult = scale.UpdateLevel(levelId, label, value);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        var (newLabel, newValue) = (level.Label, level.Value);

        if (previousLabel != newLabel)
        {
            AddKeyedDomainEvent(() => new ScoringModelScaleLevelRelabeledEvent(
                Id, Key, scaleId, levelId, previousLabel, newLabel, actor, timestamp));
        }

        if (previousValue != newValue)
        {
            AddKeyedDomainEvent(() => new ScoringModelScaleLevelValueChangedEvent(
                Id, Key, scaleId, levelId, previousValue, newValue, actor, timestamp));
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes a rating level from a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result RemoveScaleLevel(Guid scaleId, Guid levelId, EventActor actor, Instant timestamp)
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

        var level = scale.Levels.FirstOrDefault(l => l.Id == levelId);
        if (level is null)
        {
            return Result.Failure("Rating level not found.");
        }

        var removeResult = scale.RemoveLevel(levelId);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        var levelLabel = level.Label;
        AddKeyedDomainEvent(() => new ScoringModelScaleLevelRemovedEvent(Id, Key, scaleId, levelId, levelLabel, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Reorders the rating levels within a scale. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result ReorderScaleLevels(Guid scaleId, List<Guid> orderedLevelIds, EventActor actor, Instant timestamp)
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

        var previousOrder = LevelOrder(scale);

        var reorderResult = scale.ReorderLevels(orderedLevelIds);
        if (reorderResult.IsFailure)
        {
            return reorderResult;
        }

        var order = LevelOrder(scale);
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new ScoringModelScaleLevelsReorderedEvent(Id, Key, scaleId, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    private static Guid[] LevelOrder(ScoringScale scale) => [.. scale.Levels.OrderBy(l => l.Order).Select(l => l.Id)];

    #endregion Scale Level Management

    #region Output Management

    /// <summary>
    /// Adds a new output formula to the model. Only allowed when the model is in the Proposed state.
    /// The formula may reference criterion tokens and the tokens of outputs already defined. If this is
    /// the first output, it becomes primary by default; marking it primary transfers the flag.
    /// </summary>
    public Result<ScoringModelOutput> AddOutput(string name, string token, string formula, bool isPrimary, EventActor actor, Instant timestamp)
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
        var previousPrimaryId = PrimaryOutputId();

        var output = new ScoringModelOutput(Id, name, token.Trim(), formula.Trim(), makePrimary, order);
        _outputs.Add(output);

        if (makePrimary)
        {
            DemoteOtherPrimaries(output);
        }

        var (outputId, outputName, outputToken, outputFormula) = (output.Id, output.Name, output.Token, output.Formula);
        AddKeyedDomainEvent(() => new ScoringModelOutputAddedEvent(
            Id, Key, outputId, outputName, outputToken, outputFormula, order, actor, timestamp));

        RaiseIfPrimaryOutputChanged(previousPrimaryId, actor, timestamp);

        return Result.Success(output);
    }

    /// <summary>
    /// Updates an existing output. Only allowed when the model is in the Proposed state.
    /// </summary>
    public Result UpdateOutput(Guid outputId, string name, string token, string formula, bool isPrimary, EventActor actor, Instant timestamp)
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

        var beforeDetails = new ScoringOutputDetails(output.Name, output.Token);
        var previousFormula = output.Formula;
        var previousPrimaryId = PrimaryOutputId();

        var updateResult = output.Update(name, token.Trim(), formula.Trim(), makePrimary);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        if (makePrimary)
        {
            DemoteOtherPrimaries(output);
        }

        var afterDetails = new ScoringOutputDetails(output.Name, output.Token);
        if (beforeDetails != afterDetails)
        {
            AddKeyedDomainEvent(() => new ScoringModelOutputDetailsUpdatedEvent(
                Id, Key, outputId, afterDetails.Name, afterDetails.Token, beforeDetails, actor, timestamp));
        }

        var newFormula = output.Formula;
        if (previousFormula != newFormula)
        {
            AddKeyedDomainEvent(() => new ScoringModelOutputFormulaChangedEvent(
                Id, Key, outputId, previousFormula, newFormula, actor, timestamp));
        }

        RaiseIfPrimaryOutputChanged(previousPrimaryId, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Removes an output and reorders the remaining outputs. Only allowed when the model is in the
    /// Proposed state. If the primary output is removed, the first remaining output becomes primary.
    /// </summary>
    public Result RemoveOutput(Guid outputId, EventActor actor, Instant timestamp)
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
        var previousPrimaryId = PrimaryOutputId();

        _outputs.Remove(output);

        ReorderOutputsInternal();

        if (wasPrimary)
        {
            var first = _outputs.OrderBy(o => o.Order).FirstOrDefault();
            first?.SetPrimary(true);
        }

        var (outputName, outputToken) = (output.Name, output.Token);
        AddKeyedDomainEvent(() => new ScoringModelOutputRemovedEvent(Id, Key, outputId, outputName, outputToken, actor, timestamp));

        RaiseIfPrimaryOutputChanged(previousPrimaryId, actor, timestamp);

        return Result.Success();
    }

    /// <summary>
    /// Reorders the outputs based on the provided ordered list of output IDs. Only allowed when the model
    /// is in the Proposed state. The new order must keep each output's formula referencing only tokens
    /// that precede it.
    /// </summary>
    public Result ReorderOutputs(List<Guid> orderedOutputIds, EventActor actor, Instant timestamp)
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

        var previousOrder = OutputOrder();

        for (int i = 0; i < orderedOutputIds.Count; i++)
        {
            _outputs.First(o => o.Id == orderedOutputIds[i]).Order = i + 1;
        }

        var order = OutputOrder();
        if (!previousOrder.SequenceEqual(order))
        {
            AddKeyedDomainEvent(() => new ScoringModelOutputsReorderedEvent(Id, Key, previousOrder, order, actor, timestamp));
        }

        return Result.Success();
    }

    private Guid[] OutputOrder() => [.. _outputs.OrderBy(o => o.Order).Select(o => o.Id)];

    /// <summary>
    /// The primary output, or null when none is. Exactly one is required to activate, but a proposed model can
    /// have none: an edit that clears the flag on the primary leaves it so.
    /// </summary>
    private Guid? PrimaryOutputId() => _outputs.FirstOrDefault(o => o.IsPrimary)?.Id;

    private void RaiseIfPrimaryOutputChanged(Guid? previousPrimaryId, EventActor actor, Instant timestamp)
    {
        var primaryId = PrimaryOutputId();
        if (previousPrimaryId != primaryId)
        {
            AddKeyedDomainEvent(() => new ScoringModelPrimaryOutputChangedEvent(Id, Key, previousPrimaryId, primaryId, actor, timestamp));
        }
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
        EventActor actor,
        Instant timestamp,
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

        model.RaiseCreated(actor, timestamp);

        return model;
    }

    /// <summary>
    /// Raises the creation event once the first save has assigned <see cref="Key"/>. Everything else is
    /// captured now, so a caller that edits the model before that save does not rewrite its creation.
    /// </summary>
    private void RaiseCreated(EventActor actor, Instant timestamp)
    {
        var (name, description) = (Name, Description);
        ScoringScaleValues[] scales = [.. _scales.OrderBy(s => s.Order).Select(s => new ScoringScaleValues(
            s.Id,
            s.Name,
            s.Order,
            [.. s.Levels.OrderBy(l => l.Order).Select(l => new ScoringRatingLevelValues(l.Id, l.Label, l.Value, l.Order))]))];
        ScoringCriterionValues[] criteria = [.. _criteria.OrderBy(c => c.Order).Select(CriterionValues)];
        ScoringOutputValues[] outputs = [.. _outputs.OrderBy(o => o.Order)
            .Select(o => new ScoringOutputValues(o.Id, o.Name, o.Token, o.Formula, o.IsPrimary, o.Order))];

        AddPostPersistenceAction(() => AddDomainEvent(new ScoringModelCreatedEvent(
            Id, Key, name, description, scales, criteria, outputs, actor, timestamp)));
    }

    private static ScoringCriterionValues CriterionValues(ScoringModelCriterion c) =>
        new(c.Id, c.Name, c.Token, c.Description, c.Weight, c.ScaleId, c.Order);

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it.
    /// </summary>
    /// <remarks>
    /// The factory runs when the event is raised, so everything else it carries must be captured in locals by
    /// the caller — only Key may be read inside it.
    /// </remarks>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }
}
