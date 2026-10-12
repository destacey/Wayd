using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintScopeTotalsTests
{
    private static SprintScopeItem Item(SprintScopeEntry entry, SprintScopeOutcome outcome, double? estimate = 3) =>
        new(Guid.NewGuid(), entry, null, outcome, null, estimate, estimate);

    [Fact]
    public void Predictability_PartOfTheCommitmentDelivered_IsCompletedOverCommitted()
    {
        // Arrange
        var totals = SprintScopeTotals.Of(
        [
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.Completed, 5),
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.CarriedOver, 3),
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.CarriedOver, 2),
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.Descoped, 10),
        ]);

        // Act
        var byCount = totals.PredictabilityCount;
        var byEstimate = totals.PredictabilityEstimate;

        // Assert
        byCount.Should().Be(0.25);
        byEstimate.Should().Be(0.25);
    }

    [Fact]
    public void Predictability_FinishedAddedWork_StandsInForCommittedWork()
    {
        // Arrange
        var totals = SprintScopeTotals.Of(
        [
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.Completed),
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.CarriedOver),
            Item(SprintScopeEntry.Added, SprintScopeOutcome.Removed),
        ]);

        // Act
        var predictability = totals.PredictabilityCount;

        // Assert
        predictability.Should().Be(1);
        totals.SayDoCount.Should().Be(0.5);
    }

    [Fact]
    public void Predictability_MoreCompletedThanCommitted_IsCappedAtOne()
    {
        // Arrange
        var totals = SprintScopeTotals.Of(
        [
            Item(SprintScopeEntry.Committed, SprintScopeOutcome.Completed),
            Item(SprintScopeEntry.Added, SprintScopeOutcome.Completed),
            Item(SprintScopeEntry.Added, SprintScopeOutcome.Completed),
        ]);

        // Act
        var byCount = totals.PredictabilityCount;
        var byEstimate = totals.PredictabilityEstimate;

        // Assert
        byCount.Should().Be(1);
        byEstimate.Should().Be(1);
    }

    [Fact]
    public void Predictability_NothingCommitted_IsNull()
    {
        // Arrange
        var totals = SprintScopeTotals.Of([Item(SprintScopeEntry.Added, SprintScopeOutcome.Completed)]);

        // Act
        var byCount = totals.PredictabilityCount;
        var byEstimate = totals.PredictabilityEstimate;

        // Assert
        byCount.Should().BeNull();
        byEstimate.Should().BeNull();
    }

    [Fact]
    public void PredictabilityEstimate_CommittedWorkUnestimated_IsNullWhileTheCountHasOne()
    {
        // Arrange
        var totals = SprintScopeTotals.Of([Item(SprintScopeEntry.Committed, SprintScopeOutcome.Completed, estimate: null)]);

        // Act
        var byCount = totals.PredictabilityCount;
        var byEstimate = totals.PredictabilityEstimate;

        // Assert
        byCount.Should().Be(1);
        byEstimate.Should().BeNull();
    }
}
