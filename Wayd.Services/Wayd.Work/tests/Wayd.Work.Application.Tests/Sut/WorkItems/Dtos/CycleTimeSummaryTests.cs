using Wayd.Work.Application.WorkItems.Dtos;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Dtos;

public sealed class CycleTimeSummaryTests
{
    [Fact]
    public void Empty_HasZeroCountAndTotalAndNullAverage()
    {
        var empty = CycleTimeSummary.Empty;

        Assert.Equal(0, empty.WorkItemsCount);
        Assert.Equal(0d, empty.TotalCycleTimeDays);
        Assert.Null(empty.AverageCycleTimeDays);
    }

    [Fact]
    public void DefaultInstance_BehavesLikeEmpty()
    {
        // The CycleTimeSummary.Empty static is just a convenience — `new ()`
        // should produce the same logical zero-state.
        var fresh = new CycleTimeSummary();

        Assert.Equal(0, fresh.WorkItemsCount);
        Assert.Equal(0d, fresh.TotalCycleTimeDays);
        Assert.Null(fresh.AverageCycleTimeDays);
    }

    [Theory]
    [InlineData(1, 5d, 5d)]
    [InlineData(4, 18d, 4.5d)]
    [InlineData(10, 0d, 0d)] // 10 items that all closed the same day they activated
    public void AverageCycleTimeDays_DividesTotalByCount_WhenCountIsPositive(
        int count,
        double total,
        double expectedAverage)
    {
        var summary = new CycleTimeSummary
        {
            WorkItemsCount = count,
            TotalCycleTimeDays = total,
        };

        Assert.Equal(expectedAverage, summary.AverageCycleTimeDays);
    }

    [Fact]
    public void AverageCycleTimeDays_IsNull_WhenCountIsZero()
    {
        // Total is 0 too in the sole legitimate case (the empty-set identity).
        var summary = new CycleTimeSummary
        {
            WorkItemsCount = 0,
            TotalCycleTimeDays = 0d,
        };

        Assert.Null(summary.AverageCycleTimeDays);
    }

    [Fact]
    public void Combine_WithNoSummaries_ReturnsEmptyShape()
    {
        var combined = CycleTimeSummary.Combine([]);

        Assert.Equal(0, combined.WorkItemsCount);
        Assert.Equal(0d, combined.TotalCycleTimeDays);
        Assert.Null(combined.AverageCycleTimeDays);
    }

    [Fact]
    public void Combine_OfOnlyEmptySummaries_StaysEmpty()
    {
        // Sum of identity values is still the identity — don't accidentally
        // produce a non-null average from combining nothing.
        var combined = CycleTimeSummary.Combine(new[]
        {
            CycleTimeSummary.Empty,
            CycleTimeSummary.Empty,
            new CycleTimeSummary(),
        });

        Assert.Equal(0, combined.WorkItemsCount);
        Assert.Equal(0d, combined.TotalCycleTimeDays);
        Assert.Null(combined.AverageCycleTimeDays);
    }

    [Fact]
    public void Combine_SumsCountsAndTotals()
    {
        var combined = CycleTimeSummary.Combine(new[]
        {
            new CycleTimeSummary { WorkItemsCount = 2, TotalCycleTimeDays = 10d },
            new CycleTimeSummary { WorkItemsCount = 3, TotalCycleTimeDays = 9d },
        });

        Assert.Equal(5, combined.WorkItemsCount);
        Assert.Equal(19d, combined.TotalCycleTimeDays);
    }

    [Fact]
    public void Combine_ProducesTrueWeightedAverage_NotAverageOfAverages()
    {
        // Sprint A: 1 item, 50 days  → avg 50.
        // Sprint B: 9 items, 9 days  → avg 1.
        // Average-of-averages would be (50 + 1) / 2 = 25.5 — wildly wrong.
        // Weighted by count: (50 + 9) / (1 + 9) = 5.9 — what we expect.
        var a = new CycleTimeSummary { WorkItemsCount = 1, TotalCycleTimeDays = 50d };
        var b = new CycleTimeSummary { WorkItemsCount = 9, TotalCycleTimeDays = 9d };

        var combined = CycleTimeSummary.Combine(new[] { a, b });

        Assert.Equal(10, combined.WorkItemsCount);
        Assert.Equal(59d, combined.TotalCycleTimeDays);
        Assert.NotNull(combined.AverageCycleTimeDays);
        Assert.Equal(5.9d, combined.AverageCycleTimeDays!.Value, precision: 10);
    }

    [Fact]
    public void Combine_WithMixOfEmptyAndNonEmpty_IgnoresEmptyContribution()
    {
        var nonEmpty = new CycleTimeSummary { WorkItemsCount = 4, TotalCycleTimeDays = 18d };

        var combined = CycleTimeSummary.Combine(new[]
        {
            CycleTimeSummary.Empty,
            nonEmpty,
            CycleTimeSummary.Empty,
        });

        Assert.Equal(4, combined.WorkItemsCount);
        Assert.Equal(18d, combined.TotalCycleTimeDays);
        Assert.Equal(4.5d, combined.AverageCycleTimeDays);
    }

    [Fact]
    public void Combine_IsAssociative()
    {
        // Combining (a, b) then combining the result with c should equal
        // combining (a, b, c) directly.
        var a = new CycleTimeSummary { WorkItemsCount = 2, TotalCycleTimeDays = 6d };
        var b = new CycleTimeSummary { WorkItemsCount = 5, TotalCycleTimeDays = 25d };
        var c = new CycleTimeSummary { WorkItemsCount = 3, TotalCycleTimeDays = 9d };

        var stepwise = CycleTimeSummary.Combine(new[] { CycleTimeSummary.Combine(new[] { a, b }), c });
        var allAtOnce = CycleTimeSummary.Combine(new[] { a, b, c });

        Assert.Equal(allAtOnce.WorkItemsCount, stepwise.WorkItemsCount);
        Assert.Equal(allAtOnce.TotalCycleTimeDays, stepwise.TotalCycleTimeDays);
        Assert.Equal(allAtOnce.AverageCycleTimeDays, stepwise.AverageCycleTimeDays);
    }
}
