using FluentAssertions;
using Wayd.Common.Domain.Scoring;
using Wayd.Common.Domain.Scoring.Enums;
using Wayd.Common.Domain.Tests.Data;

namespace Wayd.Common.Domain.Tests.Sut.Scoring;

public class ScoringModelTests
{
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
        var model = ScoringModel.Create("WSJF", "Weighted shortest job first.");

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
            "M", "d",
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

        var result = model.Activate();

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        model.State.Should().Be(ScoringModelState.Active);
    }

    [Fact]
    public void Activate_ShouldSucceed_WhenCriteriaHaveNoScale()
    {
        // Weighted model: no scales, criteria use free numeric entry.
        var model = _faker.AsProposedWith([], WeightedCriteria, WeightedOutputs);

        var result = model.Activate();

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    [Fact]
    public void Activate_ShouldFail_WhenReferencedScaleHasFewerThanTwoLevels()
    {
        var model = _faker.AsProposedWith(
            [("Skimpy", [("Only", 1m)])],
            [("A", "A", null, "Skimpy"), ("B", "B", null, "Skimpy")],
            [("Out", "Out", "A + B", true)]);

        var result = model.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least two rating levels");
    }

    [Fact]
    public void Activate_ShouldFail_WhenNoOutputs()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one output");
    }

    [Fact]
    public void Activate_ShouldFail_WhenNoCriteria()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.Activate();

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

        var result = model.Activate();

        result.IsSuccess.Should().BeTrue();
        model.State.Should().Be(ScoringModelState.Active);
    }

    #endregion Activate

    #region Scales

    [Fact]
    public void AddScale_ShouldFail_WhenDuplicateName()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.AddScale("Impact");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public void RemoveScale_ShouldFail_WhenReferencedByCriterion()
    {
        var model = CreateWsjfModel();
        var impact = model.Scales.Single(s => s.Name == "Impact");

        var result = model.RemoveScale(impact.Id);

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

        var result = model.RemoveScale(unused.Id);

        result.IsSuccess.Should().BeTrue();
        model.Scales.Should().ContainSingle();
    }

    [Fact]
    public void AddCriterion_ShouldFail_WhenScaleNotInModel()
    {
        var model = _faker.AsProposedWith(DefaultScales, [], []);

        var result = model.AddCriterion("X", "X", null, null, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not belong to this model");
    }

    #endregion Scales

    #region Editing gated by state

    [Fact]
    public void AddOutput_ShouldFail_WhenNotProposed()
    {
        var model = _faker.AsActiveWith(DefaultScales, WsjfCriteria, WsjfOutputs);

        var result = model.AddOutput("Extra", "E", "BV", false);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CanBeDeleted_ShouldBeTrue_OnlyWhenProposed()
    {
        var model = CreateWsjfModel();
        model.CanBeDeleted().Should().BeTrue();

        model.Activate();
        model.CanBeDeleted().Should().BeFalse();
    }

    #endregion Editing gated by state

    #region Outputs

    [Fact]
    public void AddOutput_ShouldFail_WhenFormulaReferencesUnknownToken()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Bad", "B", "BV + NOPE", true);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unknown token");
    }

    [Fact]
    public void AddOutput_ShouldRejectFunctionCalls()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Bad", "B", "Sin(BV)", true);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddOutput_ShouldFail_WhenTokenCollidesWithCriterion()
    {
        var model = _faker.AsProposedWith(DefaultScales, WsjfCriteria, []);

        var result = model.AddOutput("Dup", "BV", "BV + 1", true);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already used");
    }

    [Fact]
    public void RemoveOutput_ShouldPromoteFirstRemaining_WhenPrimaryRemoved()
    {
        var model = CreateWsjfModel(); // CoD (not primary), WSJF (primary)
        var primary = model.Outputs.Single(o => o.IsPrimary);

        model.RemoveOutput(primary.Id);

        model.Outputs.Count(o => o.IsPrimary).Should().Be(1);
        model.Outputs.Single(o => o.IsPrimary).Token.Should().Be("CoD");
    }

    #endregion Outputs

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
