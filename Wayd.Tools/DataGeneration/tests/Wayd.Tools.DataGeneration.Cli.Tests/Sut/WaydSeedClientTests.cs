using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Client.Models;
using Wayd.Tools.DataGeneration.Cli.Generation;
using Wayd.Tools.DataGeneration.Cli.Seeding;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>Which desks the seed gives an estimation scale, and which it can estimate requests on.</summary>
public class WaydSeedClientTests
{
    private static readonly Guid DeskId = Guid.NewGuid();
    private static readonly Guid TShirtSizesId = Guid.NewGuid();

    private static readonly IntakeEstimationScaleChoiceDto TShirtSizes = new()
    {
        Id = TShirtSizesId,
        Key = 3,
        Name = "T-Shirt Sizes",
        VersionNumber = 2,
        EstimateValues = ["S", "M", "L"],
    };

    private static IntakeDeskModel Desk(string? scaleName = "T-Shirt Sizes") => new()
    {
        Name = "IT Desk",
        Description = "Internal systems.",
        Visibility = "Open",
        Members = [],
        EstimationScaleName = scaleName,
    };

    private static IReadOnlyList<IntakeDeskEstimationPlan> Plan(
        IntakeDeskModel desk, EstimationScaleVersionNavigationDto? pinned, params IntakeEstimationScaleChoiceDto[] choices) =>
        WaydSeedClient.PlanIntakeDeskEstimationScales(
            [desk],
            new Dictionary<string, Guid> { [desk.Name] = DeskId },
            choices,
            new Dictionary<Guid, EstimationScaleVersionNavigationDto?> { [DeskId] = pinned });

    private static EstimationScaleVersionNavigationDto PinnedTo(Guid scaleId, bool isCurrentVersion = true) => new()
    {
        Id = scaleId,
        Key = 9,
        Name = "Pinned",
        VersionId = Guid.NewGuid(),
        VersionNumber = 1,
        IsCurrentVersion = isCurrentVersion,
    };

    [Fact]
    public void PlanIntakeDeskEstimationScales_GivesAnUnpinnedDeskItsScale()
    {
        // Act
        var plans = Plan(Desk(), pinned: null, TShirtSizes);

        // Assert
        plans.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new IntakeDeskEstimationPlan("IT Desk", DeskId, TShirtSizesId, ["S", "M", "L"]));
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_EstimatesADeskAlreadyOnTheScalesCurrentVersionWithoutChangingIt()
    {
        // Act
        var plans = Plan(Desk(), PinnedTo(TShirtSizesId), TShirtSizes);

        // Assert
        plans.Should().ContainSingle().Which.PinScaleId.Should().BeNull();
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_LeavesOutADeskOnAnotherScaleOrAnOlderVersion()
    {
        // Act
        var onAnother = Plan(Desk(), PinnedTo(Guid.NewGuid()), TShirtSizes);
        var onAnOlderVersion = Plan(Desk(), PinnedTo(TShirtSizesId, isCurrentVersion: false), TShirtSizes);

        // Assert — its values are not the ones the choices list, so the import would refuse them
        onAnother.Should().BeEmpty();
        onAnOlderVersion.Should().BeEmpty();
    }

    [Fact]
    public void PlanIntakeDeskEstimationScales_LeavesOutADeskThatDoesNotEstimateOrWhoseScaleIsNotOffered()
    {
        // Act
        var notEstimating = Plan(Desk(scaleName: null), pinned: null, TShirtSizes);
        var notOffered = Plan(Desk(scaleName: "Fibonacci"), pinned: null, TShirtSizes);

        // Assert
        notEstimating.Should().BeEmpty();
        notOffered.Should().BeEmpty();
    }

    [Fact]
    public void MatchKpiCategories_AnswersTheIdsOfActiveCategoriesByNameIgnoringCase_AndLeavesTheMissingOnesOut()
    {
        // Arrange
        var flowId = Guid.NewGuid();
        KpiCategoryDto[] existing = [new() { Id = flowId, Name = "Flow", IsActive = true }];

        // Act
        var ids = WaydSeedClient.MatchKpiCategories(["flow", "Quality"], existing);

        // Assert
        ids.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, Guid>("flow", flowId));
    }

    [Fact]
    public void MatchKpiCategories_StopsTheSeed_WhenAWantedCategoryIsInactive()
    {
        // Arrange
        KpiCategoryDto[] existing = [new() { Id = Guid.NewGuid(), Name = "Flow", IsActive = false }];

        // Act
        var match = () => WaydSeedClient.MatchKpiCategories(["Flow"], existing);

        // Assert
        match.Should().Throw<SeedException>().WithMessage("*'Flow'*inactive*");
    }

    /// <summary>Items that become done on the given check of the run, counting every check of any item.</summary>
    private sealed class Arrivals(Dictionary<int, int> doneFromCheck)
    {
        private readonly Dictionary<int, int> _doneFromCheck = doneFromCheck;
        private int _checks;

        public List<int> Checked { get; } = [];
        public List<TimeSpan> Waits { get; } = [];

        public Task<bool> IsDone(int item, CancellationToken cancellationToken)
        {
            _checks++;
            Checked.Add(item);
            return Task.FromResult(_doneFromCheck.TryGetValue(item, out var from) && _checks >= from);
        }

        public Task Delay(TimeSpan wait, CancellationToken cancellationToken)
        {
            Waits.Add(wait);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task WaitUntilAll_AnswersAtOnce_WhenEverythingIsAlreadyDone()
    {
        // Arrange
        var arrivals = new Arrivals(new() { [1] = 1, [2] = 1, [3] = 1 });

        // Act
        var missing = await WaydSeedClient.WaitUntilAll([1, 2, 3], arrivals.IsDone, TimeSpan.FromMinutes(2), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert
        missing.Should().Be(0);
        arrivals.Checked.Should().Equal(1, 2, 3);
        arrivals.Waits.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitUntilAll_StopsEachRoundAtTheFirstItemNotDone()
    {
        // Arrange — item 2 lands on the fourth check, item 3 is done all along
        var arrivals = new Arrivals(new() { [1] = 1, [2] = 4, [3] = 1 });

        // Act
        var missing = await WaydSeedClient.WaitUntilAll([1, 2, 3], arrivals.IsDone, TimeSpan.FromMinutes(2), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert — item 3 is not asked after until item 2 has landed
        missing.Should().Be(0);
        arrivals.Checked.Should().Equal(1, 2, 2, 2, 3);
    }

    [Fact]
    public async Task WaitUntilAll_DoublesTheWaitWhileNothingLands_AndDropsItBackWhenSomethingDoes()
    {
        // Arrange — item 1 lands on the sixth check, item 2 on the ninth
        var arrivals = new Arrivals(new() { [1] = 6, [2] = 9 });

        // Act
        await WaydSeedClient.WaitUntilAll([1, 2], arrivals.IsDone, TimeSpan.FromMinutes(2), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert
        arrivals.Waits.Select(w => w.TotalSeconds).Should().Equal(1, 2, 4, 8, 8, 1, 2);
    }

    [Fact]
    public async Task WaitUntilAll_KeepsWaiting_WhenAnItemBehindTheFirstHasLanded()
    {
        // Arrange — item 1 never lands; item 3 has, out of order, so the first stall check finds progress
        var arrivals = new Arrivals(new() { [3] = 1 });

        // Act
        await WaydSeedClient.WaitUntilAll([1, 2, 3, 4], arrivals.IsDone, TimeSpan.FromSeconds(10), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert — the stall clock starts again after the pass that found item 3
        arrivals.Waits.Select(w => w.TotalSeconds).Should().Equal(1, 2, 4, 8, 1, 2, 4, 8);
    }

    [Fact]
    public async Task WaitUntilAll_CountsEveryItemStillNotDone_OnceNothingLandsForTheStallTimeout()
    {
        // Arrange — items 1, 2 and 4 never land
        var arrivals = new Arrivals(new() { [3] = 1 });

        // Act
        var missing = await WaydSeedClient.WaitUntilAll([1, 2, 3, 4], arrivals.IsDone, TimeSpan.FromSeconds(10), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert — the last pass checks every item still pending, so the count is exact
        missing.Should().Be(3);
        arrivals.Checked.Should().EndWith([1, 2, 4]);
    }

    [Fact]
    public async Task WaitUntilAll_AnswersNoneMissing_WhenTheStallCheckFindsEveryItemHasLanded()
    {
        // Arrange — items 2 and 3 have landed behind item 1, which lands on the sixth check: the first of the stall pass
        var arrivals = new Arrivals(new() { [1] = 6, [2] = 1, [3] = 1 });

        // Act
        var missing = await WaydSeedClient.WaitUntilAll([1, 2, 3], arrivals.IsDone, TimeSpan.FromSeconds(10), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert — five rounds stop at item 1, then the stall pass checks all three and finds none missing
        missing.Should().Be(0);
        arrivals.Checked.Should().Equal(1, 1, 1, 1, 1, 1, 2, 3);
    }

    [Fact]
    public async Task WaitUntilAll_KeepsWaitingPastTheStallTimeout_WhileItemsKeepLanding()
    {
        // Arrange — ten items, each landing seven checks after the one before: far longer in total than the
        // stall timeout, but never a stall
        var arrivals = new Arrivals(Enumerable.Range(1, 10).ToDictionary(i => i, i => i * 7));

        // Act
        var missing = await WaydSeedClient.WaitUntilAll([.. Enumerable.Range(1, 10)], arrivals.IsDone, TimeSpan.FromSeconds(30), arrivals.Delay, TestContext.Current.CancellationToken);

        // Assert
        missing.Should().Be(0);
        arrivals.Waits.Sum(w => w.TotalSeconds).Should().BeGreaterThan(30);
    }
}
