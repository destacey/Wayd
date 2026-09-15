using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Scoring;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.Scoring;

public class ScoringModelTests
{
    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 30);

    private readonly ScoringModelFaker _faker = new();

    // One shared scale used by the criteria below.
    private static readonly (string Name, (string Label, decimal Value)[] Levels)[] DefaultScales =
    [
        ("Impact", [("High", 8m), ("Medium", 5m), ("Low", 1m)])
    ];

    // A WSJF model: rated criteria BV/TC/RR/JS, an intermediate CoD output, and a primary WSJF output.
    private static readonly (string Name, string Token, decimal? Weight, string? ScaleName)[] WsjfCriteria =
    [
        ("Business Value", "BV", null, "Impact"),
        ("Time Criticality", "TC", null, "Impact"),
        ("Risk Reduction", "RR", null, "Impact"),
        ("Job Size", "JS", null, "Impact")
    ];

    private static readonly (string Name, string Token, string Formula, bool IsPrimary)[] WsjfOutputs =
    [
        ("Cost of Delay", "CoD", "BV + TC + RR", false),
        ("WSJF", "WSJF", "CoD / JS", true)
    ];

    // A weighted-sum model expressed as a single primary output (regression against the old behavior).
    private static readonly (string Name, string Token, decimal? Weight, string? ScaleName)[] WeightedCriteria =
    [
        ("Strategic Alignment", "SA", 60m, null),
        ("ROI Potential", "ROI", 40m, null)
    ];

    private static readonly (string Name, string Token, string Formula, bool IsPrimary)[] WeightedOutputs =
    [
        ("Weighted Score", "Score", "(SA * 60 + ROI * 40) / 100", true)
    ];

    private ScoringModel CreateWsjfModel()
        => _faker.AsProposedWith(DefaultScales, WsjfCriteria, WsjfOutputs);

    #region Create

    [Fact]
    public void Create_ShouldCreateProposedModelWithoutChildren()
    {
        var model = ScoringModel.Create("WSJF", "Weighted shortest job first.", EventActor.System, At);

        model.Should().NotBeNull();
        model.Name.Should().Be("WSJF");
        model.State.Should().Be(ScoringModelState.Proposed);
        model.Criteria.Should().BeEmpty();
        model.Scales.Should().BeEmpty();
        model.Outputs.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldResolveCriterionScaleByName()
    {
        var model = CreateWsjfModel();

        var impact = model.Scales.Single(s => s.Name == "Impact");
        model.Criteria.Should().OnlyContain(c => c.ScaleId == impact.Id);
        impact.Levels.Should().HaveCount(3);
    }

    [Fact]
    public void Create_ShouldDefaultFirstOutputToPrimary_WhenNoneFlagged()
    {
        var model = ScoringModel.Create(
            "M", "d", EventActor.System, At,
            outputs: [("A", "A", "1", false), ("B", "B", "2", false)]);

        model.Outputs.Count(o => o.IsPrimary).Should().Be(1);
        model.Outputs.OrderBy(o => o.Order).First().IsPrimary.Should().BeTrue();
    }

    #endregion Create

    #region Activate

    [Fact]
    public void Activate_ShouldSucceed_ForValidWsjfModel()
    {
        var model = CreateWsjfModel();

        var result = model.Activate(EventActor.System, At);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.State.Should().Be(ScoringModelState.Active);
    }

    [Fact]
    public void Activate_ShouldSucceed_WhenCriteriaHaveNoScale()
    {
        // Weighted model: no scales, criteria use free numeric entry.
        var model = _faker.AsProposedWith([], WeightedCriteria, WeightedOutputs);

        var result = model.Activate(EventActor.System, At);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public void Activate_ShouldFail_WhenReferencedScaleHasFewerThanTwoLevels()
    {
        var model = _faker.AsProposedWith(
            [("Skimpy", [("Only", 1m)])],
            [("A", "A", null, "Skimpy"), ("B", "B", null, "Skimpy")],
            [("Out", "Out", "A + B", true)]);

        var result = model.Activate(EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least two rating levels");
    }

    [Fact]
    public void Activate_ShouldFail_WhenNoOutputs()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.Activate(EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one output");
    }

    [Fact]
    public void Activate_ShouldFail_WhenNoCriteria()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.Activate(EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one criterion");
    }

    [Fact]
    public void Activate_ShouldSucceed_WithSingleCriterion()
    {
        var model = _faker.AsProposedWith(
            DefaultScales,
            [("Only", "X", null, "Impact")],
            [("Out", "Y", "X", true)]);

        var result = model.Activate(EventActor.System, At);

        result.IsSuccess.Should().BeTrue();
        model.State.Should().Be(ScoringModelState.Active);
    }

    #endregion Activate

    #region Scales

    [Fact]
    public void AddScale_ShouldFail_WhenDuplicateName()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.AddScale("Impact", EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void RemoveScale_ShouldFail_WhenReferencedByCriterion()
    {
        var model = CreateWsjfModel();
        var impact = model.Scales.Single(s => s.Name == "Impact");

        var result = model.RemoveScale(impact.Id, EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("referenced");
    }

    [Fact]
    public void RemoveScale_ShouldSucceed_WhenUnreferenced()
    {
        var model = _faker.AsProposedWith(
            [("Impact", [("High", 8m), ("Low", 1m)]), ("Unused", [("A", 1m), ("B", 2m)])],
            [("A", "A", null, "Impact"), ("B", "B", null, "Impact")],
            [("Out", "Out", "A + B", true)]);
        var unused = model.Scales.Single(s => s.Name == "Unused");

        var result = model.RemoveScale(unused.Id, EventActor.System, At);

        result.IsSuccess.Should().BeTrue();
        model.Scales.Should().ContainSingle();
    }

    [Fact]
    public void AddCriterion_ShouldFail_WhenScaleNotInModel()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.AddCriterion("X", "X", null, null, Guid.NewGuid(), EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not belong to this model");
    }

    #endregion Scales

    #region Editing gated by state

    [Fact]
    public void AddOutput_ShouldFail_WhenNotProposed()
    {
        var model = _faker.AsActiveWith(DefaultScales, WsjfCriteria, WsjfOutputs);

        var result = model.AddOutput("Extra", "E", "BV", false, EventActor.System, At);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanBeDeleted_ShouldBeTrue_OnlyWhenProposed()
    {
        var model = CreateWsjfModel();
        model.CanBeDeleted().Should().BeTrue();

        model.Activate(EventActor.System, At);
        model.CanBeDeleted().Should().BeFalse();
    }

    #endregion Editing gated by state

    #region Outputs

    [Fact]
    public void AddOutput_ShouldFail_WhenFormulaReferencesUnknownToken()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Bad", "B", "BV + NOPE", true, EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unknown token");
    }

    [Fact]
    public void AddOutput_ShouldRejectFunctionCalls()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Bad", "B", "Sin(BV)", true, EventActor.System, At);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddOutput_ShouldFail_WhenTokenCollidesWithCriterion()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Dup", "BV", "BV + 1", true, EventActor.System, At);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already used");
    }

    [Fact]
    public void RemoveOutput_ShouldPromoteFirstRemaining_WhenPrimaryRemoved()
    {
        var model = CreateWsjfModel(); // CoD (not primary), WSJF (primary)
        var primary = model.Outputs.Single(o => o.IsPrimary);

        model.RemoveOutput(primary.Id, EventActor.System, At);

        model.Outputs.Count(o => o.IsPrimary).Should().Be(1);
        model.Outputs.Single(o => o.IsPrimary).Token.Should().Be("CoD");
    }

    #endregion Outputs

    #region Events

    private static ScoringModel Saved(ScoringModel model)
    {
        model.SetPrivate(m => m.Key, 7);
        model.ExecutePostPersistenceActions();
        model.ClearDomainEvents();

        return model;
    }

    private ScoringModel SavedWsjfModel() => Saved(CreateWsjfModel());

    private static ScoringModel CreateWithChildren() => ScoringModel.Create(
        "WSJF", "Weighted shortest job first.", EventActor.System, At,
        scales: [("Impact", [("High", 8m), ("Low", 1m)])],
        criteria: [("Business Value", "BV", "Value delivered.", 2m, "Impact"), ("Job Size", "JS", null, null, null)],
        outputs: [("WSJF", "WSJF", "BV / JS", false)]);

    [Fact]
    public void Create_RaisesCreated_OnceTheFirstSaveAssignsTheKey()
    {
        // Arrange & Act
        var model = CreateWithChildren();

        // Assert
        model.DomainEvents.Should().BeEmpty();

        model.SetPrivate(m => m.Key, 7);
        model.ExecutePostPersistenceActions();

        var created = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelCreatedEvent>().Subject;
        created.Id.Should().Be(model.Id);
        created.Key.Should().Be(7);
        created.Name.Should().Be("WSJF");
        created.Description.Should().Be("Weighted shortest job first.");

        var scale = model.Scales.Single();
        var impact = created.Scales.Should().ContainSingle().Subject;
        impact.ScaleId.Should().Be(scale.Id);
        impact.Levels.Select(l => (l.LevelId, l.Label, l.Value, l.Order))
            .Should().Equal(scale.Levels.OrderBy(l => l.Order).Select(l => (l.Id, l.Label, l.Value, l.Order)));

        created.Criteria.Should().HaveCount(2);
        created.Criteria[0].Should().Be(new ScoringCriterionValues(
            model.Criteria.Single(c => c.Token == "BV").Id, "Business Value", "BV", "Value delivered.", 2m, scale.Id, 1));
        created.Criteria[1].ScaleId.Should().BeNull();

        var output = created.Outputs.Should().ContainSingle().Subject;
        output.Should().Be(new ScoringOutputValues(model.Outputs.Single().Id, "WSJF", "WSJF", "BV / JS", true, 1));
    }

    [Fact]
    public void Create_ThenEditedBeforeTheFirstSave_RecordsTheModelAsCreated()
    {
        // Arrange
        var model = ScoringModel.Create("WSJF", "Weighted shortest job first.", EventActor.System, At);

        // Act
        model.Update("WSJF v2", "Revised.", EventActor.System, At);
        model.AddScale("Impact", EventActor.System, At);

        // Assert
        model.DomainEvents.Should().BeEmpty();

        model.SetPrivate(m => m.Key, 7);
        model.ExecutePostPersistenceActions();

        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelCreatedEvent),
            typeof(ScoringModelDetailsUpdatedEvent),
            typeof(ScoringModelScaleAddedEvent));

        var created = (ScoringModelCreatedEvent)model.DomainEvents.First();
        created.Name.Should().Be("WSJF");
        created.Scales.Should().BeEmpty("the scale was added after the model was created");
        model.DomainEvents.Cast<IAggregateEvent>().Should().OnlyContain(e => e.AggregateId == model.Id);
        model.DomainEvents.OfType<ScoringModelScaleAddedEvent>().Single().Key.Should().Be(7);
    }

    [Fact]
    public void Update_RaisesDetailsUpdated_CarryingWhatItReplaced()
    {
        // Arrange
        var model = SavedWsjfModel();
        var previous = new ScoringModelDetails(model.Name, model.Description);

        // Act
        var result = model.Update("WSJF v2", "Revised.", EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelDetailsUpdatedEvent>().Subject;
        updated.Key.Should().Be(7);
        updated.Name.Should().Be("WSJF v2");
        updated.Description.Should().Be("Revised.");
        updated.Previous.Should().Be(previous);
    }

    [Fact]
    public void Update_WithDetailsThatMatchOnceTrimmed_RaisesNothing()
    {
        // Arrange
        var model = SavedWsjfModel();

        // Act
        var result = model.Update($"{model.Name}  ", $" {model.Description}", EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_RaisesActivated()
    {
        // Arrange
        var model = SavedWsjfModel();

        // Act
        model.Activate(EventActor.System, At);

        // Assert
        var activated = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelActivatedEvent>().Subject;
        activated.Id.Should().Be(model.Id);
        activated.Key.Should().Be(7);
    }

    [Fact]
    public void Activate_WhenInvalid_RaisesNothing()
    {
        // Arrange
        var model = Saved(_faker.AsProposedWith(DefaultScales, WsjfCriteria, []));

        // Act
        var result = model.Activate(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Archive_RaisesArchived()
    {
        // Arrange
        var model = Saved(_faker.AsActiveWith(DefaultScales, WsjfCriteria, WsjfOutputs));

        // Act
        model.Archive(EventActor.System, At);

        // Assert
        model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelArchivedEvent>()
            .Which.Key.Should().Be(7);
    }

    [Fact]
    public void Delete_RaisesDeleted_CarryingTheName()
    {
        // Arrange
        var model = SavedWsjfModel();

        // Act
        var result = model.Delete(EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelDeletedEvent>().Subject;
        deleted.Key.Should().Be(7);
        deleted.Name.Should().Be(model.Name);
    }

    [Fact]
    public void Delete_WhenActive_FailsAndRaisesNothing()
    {
        // Arrange
        var model = Saved(_faker.AsActiveWith(DefaultScales, WsjfCriteria, WsjfOutputs));

        // Act
        var result = model.Delete(EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AMutationTheStateRefuses_RaisesNothing()
    {
        // Arrange
        var model = Saved(_faker.AsActiveWith(DefaultScales, WsjfCriteria, WsjfOutputs));

        // Act
        var result = model.AddOutput("Extra", "E", "BV", false, EventActor.System, At);

        // Assert
        result.IsFailure.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddCriterion_RaisesCriterionAdded()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();

        // Act
        var criterion = model.AddCriterion(" Customer Reach ", "CR", "Who it reaches.", 1.5m, impact.Id, EventActor.System, At).Value;

        // Assert
        var added = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelCriterionAddedEvent>().Subject;
        added.CriterionId.Should().Be(criterion.Id);
        added.Name.Should().Be("Customer Reach");
        added.Token.Should().Be("CR");
        added.Description.Should().Be("Who it reaches.");
        added.Weight.Should().Be(1.5m);
        added.ScaleId.Should().Be(impact.Id);
        added.Order.Should().Be(5);
    }

    [Fact]
    public void UpdateCriterion_RaisesAnEventForEachPartThatChanged()
    {
        // Arrange
        var model = SavedWsjfModel();
        var bv = model.Criteria.Single(c => c.Token == "BV");

        // Act
        model.UpdateCriterion(bv.Id, "Business Worth", "BV", bv.Description, 3m, null, EventActor.System, At);

        // Assert
        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelCriterionDetailsUpdatedEvent),
            typeof(ScoringModelCriterionWeightChangedEvent),
            typeof(ScoringModelCriterionScaleChangedEvent));

        var details = model.DomainEvents.OfType<ScoringModelCriterionDetailsUpdatedEvent>().Single();
        details.CriterionId.Should().Be(bv.Id);
        details.Name.Should().Be("Business Worth");
        details.Previous.Should().Be(new ScoringCriterionDetails("Business Value", "BV", null));

        var weight = model.DomainEvents.OfType<ScoringModelCriterionWeightChangedEvent>().Single();
        weight.PreviousWeight.Should().BeNull();
        weight.Weight.Should().Be(3m);

        var scale = model.DomainEvents.OfType<ScoringModelCriterionScaleChangedEvent>().Single();
        scale.PreviousScaleId.Should().Be(model.Scales.Single().Id);
        scale.ScaleId.Should().BeNull();
    }

    [Fact]
    public void UpdateCriterion_WithNothingChanged_RaisesNothing()
    {
        // Arrange
        var model = SavedWsjfModel();
        var bv = model.Criteria.Single(c => c.Token == "BV");

        // Act
        var result = model.UpdateCriterion(bv.Id, $" {bv.Name} ", bv.Token, bv.Description, bv.Weight, bv.ScaleId, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveCriterion_RaisesCriterionRemoved_CarryingItsNameAndToken()
    {
        // Arrange
        var model = SavedWsjfModel();
        var rr = model.Criteria.Single(c => c.Token == "RR");

        // Act
        model.RemoveCriterion(rr.Id, EventActor.System, At);

        // Assert
        var removed = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelCriterionRemovedEvent>().Subject;
        removed.CriterionId.Should().Be(rr.Id);
        removed.Name.Should().Be("Risk Reduction");
        removed.Token.Should().Be("RR");
    }

    [Fact]
    public void ReorderCriteria_RaisesReordered_CarryingBothOrders()
    {
        // Arrange
        var model = SavedWsjfModel();
        var previous = model.Criteria.OrderBy(c => c.Order).Select(c => c.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        model.ReorderCriteria(reversed, EventActor.System, At);

        // Assert
        var reordered = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelCriteriaReorderedEvent>().Subject;
        reordered.PreviousOrder.Should().Equal(previous);
        reordered.Order.Should().Equal(reversed);
    }

    [Fact]
    public void ReorderCriteria_IntoTheSameOrder_RaisesNothing()
    {
        // Arrange
        var model = SavedWsjfModel();
        var current = model.Criteria.OrderBy(c => c.Order).Select(c => c.Id).ToList();

        // Act
        model.ReorderCriteria(current, EventActor.System, At);

        // Assert
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddScale_RaisesScaleAdded()
    {
        // Arrange
        var model = SavedWsjfModel();

        // Act
        var scale = model.AddScale(" Effort ", EventActor.System, At).Value;

        // Assert
        var added = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleAddedEvent>().Subject;
        added.ScaleId.Should().Be(scale.Id);
        added.Name.Should().Be("Effort");
        added.Order.Should().Be(2);
    }

    [Fact]
    public void UpdateScale_RaisesRenamed_OnlyWhenTheNameChanged()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();

        // Act
        model.UpdateScale(impact.Id, "Impact ", EventActor.System, At);
        model.UpdateScale(impact.Id, "Value", EventActor.System, At);

        // Assert
        var renamed = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleRenamedEvent>().Subject;
        renamed.ScaleId.Should().Be(impact.Id);
        renamed.PreviousName.Should().Be("Impact");
        renamed.Name.Should().Be("Value");
    }

    [Fact]
    public void RemoveScale_RaisesScaleRemoved_CarryingItsName()
    {
        // Arrange
        var model = Saved(_faker.AsProposedWith([("Unused", [("A", 1m)])], [], []));
        var unused = model.Scales.Single();

        // Act
        model.RemoveScale(unused.Id, EventActor.System, At);

        // Assert
        var removed = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleRemovedEvent>().Subject;
        removed.ScaleId.Should().Be(unused.Id);
        removed.Name.Should().Be("Unused");
    }

    [Fact]
    public void ReorderScales_RaisesReordered_CarryingBothOrders()
    {
        // Arrange
        var model = Saved(_faker.AsProposedWith([("First", []), ("Second", [])], [], []));
        var previous = model.Scales.OrderBy(s => s.Order).Select(s => s.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        model.ReorderScales(reversed, EventActor.System, At);

        // Assert
        var reordered = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScalesReorderedEvent>().Subject;
        reordered.PreviousOrder.Should().Equal(previous);
        reordered.Order.Should().Equal(reversed);
    }

    [Fact]
    public void AddScaleLevel_RaisesScaleLevelAdded()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();

        // Act
        var level = model.AddScaleLevel(impact.Id, "Critical", 13m, EventActor.System, At).Value;

        // Assert
        var added = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleLevelAddedEvent>().Subject;
        added.ScaleId.Should().Be(impact.Id);
        added.LevelId.Should().Be(level.Id);
        added.Label.Should().Be("Critical");
        added.Value.Should().Be(13m);
        added.Order.Should().Be(4);
    }

    [Fact]
    public void UpdateScaleLevel_RaisesRelabeledAndValueChanged_ForWhatChanged()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();
        var high = impact.Levels.Single(l => l.Label == "High");

        // Act
        model.UpdateScaleLevel(impact.Id, high.Id, "Very High", 8m, EventActor.System, At);
        model.UpdateScaleLevel(impact.Id, high.Id, "Very High", 9m, EventActor.System, At);

        // Assert
        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelScaleLevelRelabeledEvent),
            typeof(ScoringModelScaleLevelValueChangedEvent));

        var relabeled = model.DomainEvents.OfType<ScoringModelScaleLevelRelabeledEvent>().Single();
        relabeled.LevelId.Should().Be(high.Id);
        relabeled.PreviousLabel.Should().Be("High");
        relabeled.Label.Should().Be("Very High");

        var valueChanged = model.DomainEvents.OfType<ScoringModelScaleLevelValueChangedEvent>().Single();
        valueChanged.PreviousValue.Should().Be(8m);
        valueChanged.Value.Should().Be(9m);
    }

    [Fact]
    public void RemoveScaleLevel_RaisesScaleLevelRemoved_CarryingItsLabel()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();
        var low = impact.Levels.Single(l => l.Label == "Low");

        // Act
        model.RemoveScaleLevel(impact.Id, low.Id, EventActor.System, At);

        // Assert
        var removed = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleLevelRemovedEvent>().Subject;
        removed.ScaleId.Should().Be(impact.Id);
        removed.LevelId.Should().Be(low.Id);
        removed.Label.Should().Be("Low");
    }

    [Fact]
    public void ReorderScaleLevels_RaisesReordered_CarryingBothOrders()
    {
        // Arrange
        var model = SavedWsjfModel();
        var impact = model.Scales.Single();
        var previous = impact.Levels.OrderBy(l => l.Order).Select(l => l.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        model.ReorderScaleLevels(impact.Id, reversed, EventActor.System, At);

        // Assert
        var reordered = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelScaleLevelsReorderedEvent>().Subject;
        reordered.ScaleId.Should().Be(impact.Id);
        reordered.PreviousOrder.Should().Equal(previous);
        reordered.Order.Should().Equal(reversed);
    }

    [Fact]
    public void AddOutput_TheFirst_RaisesAddedAndThePrimaryMovingToIt()
    {
        // Arrange
        var model = Saved(_faker.AsProposedWith(DefaultScales, WsjfCriteria, []));

        // Act
        var output = model.AddOutput("Cost of Delay", "CoD", "BV + TC + RR", false, EventActor.System, At).Value;

        // Assert
        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelOutputAddedEvent),
            typeof(ScoringModelPrimaryOutputChangedEvent));

        var added = model.DomainEvents.OfType<ScoringModelOutputAddedEvent>().Single();
        added.OutputId.Should().Be(output.Id);
        added.Name.Should().Be("Cost of Delay");
        added.Token.Should().Be("CoD");
        added.Formula.Should().Be("BV + TC + RR");
        added.Order.Should().Be(1);

        var primary = model.DomainEvents.OfType<ScoringModelPrimaryOutputChangedEvent>().Single();
        primary.PreviousOutputId.Should().BeNull();
        primary.OutputId.Should().Be(output.Id);
    }

    [Fact]
    public void AddOutput_NotPrimary_RaisesOnlyAdded()
    {
        // Arrange
        var model = SavedWsjfModel();

        // Act
        model.AddOutput("Doubled", "Doubled", "WSJF * 2", false, EventActor.System, At);

        // Assert
        model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelOutputAddedEvent>();
    }

    [Fact]
    public void AddOutput_MarkedPrimary_RaisesThePrimaryMovingFromTheOldOne()
    {
        // Arrange
        var model = SavedWsjfModel();
        var wsjf = model.Outputs.Single(o => o.IsPrimary);

        // Act
        var doubled = model.AddOutput("Doubled", "Doubled", "WSJF * 2", true, EventActor.System, At).Value;

        // Assert
        var primary = model.DomainEvents.OfType<ScoringModelPrimaryOutputChangedEvent>().Should().ContainSingle().Subject;
        primary.PreviousOutputId.Should().Be(wsjf.Id);
        primary.OutputId.Should().Be(doubled.Id);
    }

    [Fact]
    public void UpdateOutput_RaisesAnEventForEachPartThatChanged()
    {
        // Arrange
        var model = SavedWsjfModel();
        var cod = model.Outputs.Single(o => o.Token == "CoD");
        var wsjf = model.Outputs.Single(o => o.Token == "WSJF");

        // Act
        model.UpdateOutput(cod.Id, "Delay Cost", "CoD", "BV + TC", true, EventActor.System, At);

        // Assert
        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelOutputDetailsUpdatedEvent),
            typeof(ScoringModelOutputFormulaChangedEvent),
            typeof(ScoringModelPrimaryOutputChangedEvent));

        var details = model.DomainEvents.OfType<ScoringModelOutputDetailsUpdatedEvent>().Single();
        details.OutputId.Should().Be(cod.Id);
        details.Name.Should().Be("Delay Cost");
        details.Previous.Should().Be(new ScoringOutputDetails("Cost of Delay", "CoD"));

        var formula = model.DomainEvents.OfType<ScoringModelOutputFormulaChangedEvent>().Single();
        formula.PreviousFormula.Should().Be("BV + TC + RR");
        formula.Formula.Should().Be("BV + TC");

        var primary = model.DomainEvents.OfType<ScoringModelPrimaryOutputChangedEvent>().Single();
        primary.PreviousOutputId.Should().Be(wsjf.Id);
        primary.OutputId.Should().Be(cod.Id);
    }

    [Fact]
    public void UpdateOutput_WithNothingChanged_RaisesNothing()
    {
        // Arrange
        var model = SavedWsjfModel();
        var wsjf = model.Outputs.Single(o => o.IsPrimary);

        // Act
        var result = model.UpdateOutput(wsjf.Id, wsjf.Name, wsjf.Token, $" {wsjf.Formula} ", true, EventActor.System, At);

        // Assert
        result.IsSuccess.Should().BeTrue();
        model.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveOutput_ThePrimary_RaisesRemovedAndThePrimaryMovingToTheFirstRemaining()
    {
        // Arrange
        var model = SavedWsjfModel();
        var wsjf = model.Outputs.Single(o => o.IsPrimary);
        var cod = model.Outputs.Single(o => o.Token == "CoD");

        // Act
        model.RemoveOutput(wsjf.Id, EventActor.System, At);

        // Assert
        model.DomainEvents.Select(e => e.GetType()).Should().Equal(
            typeof(ScoringModelOutputRemovedEvent),
            typeof(ScoringModelPrimaryOutputChangedEvent));

        var removed = model.DomainEvents.OfType<ScoringModelOutputRemovedEvent>().Single();
        removed.OutputId.Should().Be(wsjf.Id);
        removed.Name.Should().Be("WSJF");
        removed.Token.Should().Be("WSJF");

        var primary = model.DomainEvents.OfType<ScoringModelPrimaryOutputChangedEvent>().Single();
        primary.PreviousOutputId.Should().Be(wsjf.Id);
        primary.OutputId.Should().Be(cod.Id);
    }

    [Fact]
    public void ReorderOutputs_RaisesReordered_CarryingBothOrders()
    {
        // Arrange
        var model = Saved(_faker.AsProposedWith([], WeightedCriteria,
            [("First", "First", "SA", false), ("Second", "Second", "ROI", true)]));
        var previous = model.Outputs.OrderBy(o => o.Order).Select(o => o.Id).ToList();
        var reversed = Enumerable.Reverse(previous).ToList();

        // Act
        model.ReorderOutputs(reversed, EventActor.System, At);

        // Assert
        var reordered = model.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ScoringModelOutputsReorderedEvent>().Subject;
        reordered.PreviousOrder.Should().Equal(previous);
        reordered.Order.Should().Equal(reversed);
    }

    #endregion Events

    #region CalculateScore

    [Fact]
    public void CalculateScore_ShouldComputeWsjfChain_AndReturnAllOutputs()
    {
        var model = CreateWsjfModel();
        var bv = model.Criteria.Single(c => c.Token == "BV");
        var tc = model.Criteria.Single(c => c.Token == "TC");
        var rr = model.Criteria.Single(c => c.Token == "RR");
        var js = model.Criteria.Single(c => c.Token == "JS");

        // CoD = 8 + 5 + 3 = 16 ; WSJF = 16 / 5 = 3.2
        var result = model.CalculateScore(new Dictionary<Guid, decimal>
        {
            [bv.Id] = 8m,
            [tc.Id] = 5m,
            [rr.Id] = 3m,
            [js.Id] = 5m
        });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.OutputValues["CoD"].Should().Be(16m);
        result.Value.OutputValues["WSJF"].Should().Be(3.2m);
        result.Value.PrimaryValue.Should().Be(3.2m);
    }

    [Fact]
    public void CalculateScore_ShouldMatchWeightedSum_Regression()
    {
        var model = _faker.AsProposedWith([], WeightedCriteria, WeightedOutputs);
        var sa = model.Criteria.Single(c => c.Token == "SA");
        var roi = model.Criteria.Single(c => c.Token == "ROI");

        // (5*60 + 2*40) / 100 = (300 + 80)/100 = 3.8
        var result = model.CalculateScore(new Dictionary<Guid, decimal>
        {
            [sa.Id] = 5m,
            [roi.Id] = 2m
        });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.PrimaryValue.Should().Be(3.8m);
    }

    [Fact]
    public void CalculateScore_ShouldFail_WhenCriterionUnrated()
    {
        var model = CreateWsjfModel();
        var bv = model.Criteria.Single(c => c.Token == "BV");

        var result = model.CalculateScore(new Dictionary<Guid, decimal> { [bv.Id] = 8m });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not been rated");
    }

    [Fact]
    public void CalculateScore_ShouldFail_WhenDivisionByZero()
    {
        var model = CreateWsjfModel();
        var bv = model.Criteria.Single(c => c.Token == "BV");
        var tc = model.Criteria.Single(c => c.Token == "TC");
        var rr = model.Criteria.Single(c => c.Token == "RR");
        var js = model.Criteria.Single(c => c.Token == "JS");

        var result = model.CalculateScore(new Dictionary<Guid, decimal>
        {
            [bv.Id] = 8m,
            [tc.Id] = 5m,
            [rr.Id] = 3m,
            [js.Id] = 0m // JS = 0 → WSJF divides by zero
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("divided by zero");
    }

    #endregion CalculateScore
}
