using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models.BacklogHealth;

namespace Wayd.Work.Domain.Tests.Sut.Models.BacklogHealth;

public class BacklogHealthAssessorTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 9, 23, 12, 0);

    // Ten completions over 70 days is one item a week, so runway in weeks equals the backlog
    // size and the default four-week readiness window holds four items.
    private const int LookbackDays = 70;

    private static BacklogHealthItem Item(int rank, WorkStatusCategory status = WorkStatusCategory.Proposed) => new()
    {
        Id = Guid.NewGuid(),
        Rank = rank,
        StatusCategory = status,
        StoryPoints = 3,
        Created = _now - Duration.FromDays(10),
        LastModified = _now - Duration.FromDays(1),
        Activated = status == WorkStatusCategory.Active ? _now - Duration.FromDays(1) : null,
        IsAssigned = true,
        HasParent = true,
        HasProject = true,
    };

    private static List<BacklogHealthItem> Items(int count, WorkStatusCategory status = WorkStatusCategory.Proposed) =>
        [.. Enumerable.Range(1, count).Select(rank => Item(rank, status))];

    private static BacklogHealthCompletion Completion(double cycleTimeDays = 5, double? storyPoints = 3) =>
        new(_now - Duration.FromDays(cycleTimeDays + 1), _now - Duration.FromDays(1), storyPoints);

    private static BacklogHealthHistory History(int completions = 10, int itemsCreated = 10) =>
        new(LookbackDays, [.. Enumerable.Range(0, completions).Select(_ => Completion())], itemsCreated);

    private static BacklogHealthHistory History(IReadOnlyList<BacklogHealthCompletion> completions, int itemsCreated = 10) =>
        new(LookbackDays, completions, itemsCreated);

    private static BacklogHealthAssessment Assess(
        IReadOnlyCollection<BacklogHealthItem> backlog,
        BacklogHealthHistory? history = null,
        int? memberCount = 5,
        bool usesProjects = true,
        BacklogHealthThresholds? thresholds = null) =>
        BacklogHealthAssessor.Assess(backlog, history ?? History(), memberCount, usesProjects, _now, thresholds ?? BacklogHealthThresholds.Default);

    [Fact]
    public void Assess_ReturnsOneResultPerCheckInOrder()
    {
        // Arrange
        var backlog = Items(5);

        // Act
        var result = Assess(backlog);

        // Assert
        result.Checks.Select(c => c.Check).Should().Equal(Enum.GetValues<BacklogHealthCheck>());
        result.ItemFlags.Keys.Should().BeEquivalentTo(backlog.Select(i => i.Id));
    }

    [Theory]
    [InlineData(1, HealthStatus.Unhealthy)]
    [InlineData(3, HealthStatus.AtRisk)]
    [InlineData(10, HealthStatus.Healthy)]
    [InlineData(26, HealthStatus.Healthy)]
    [InlineData(27, HealthStatus.AtRisk)]
    public void Assess_Runway_GradesWeeksOfBacklogAtRecentThroughput(int backlogItems, HealthStatus expected)
    {
        // Arrange
        var backlog = Items(backlogItems);

        // Act
        var result = Assess(backlog);

        // Assert
        var runway = result[BacklogHealthCheck.Runway];
        runway.Grade.Should().Be(expected);
        runway.Value.Should().BeApproximately(backlogItems, 0.0001);
    }

    [Fact]
    public void Assess_TooFewCompletions_DoesNotGradeHistoryChecks()
    {
        // Arrange
        var backlog = Items(5);

        // Act
        var result = Assess(backlog, History(completions: BacklogHealthAssessor.MinimumItemsCompleted - 1));

        // Assert
        foreach (var check in new[] { BacklogHealthCheck.Runway, BacklogHealthCheck.AgingWip, BacklogHealthCheck.Oversized })
        {
            result[check].Outcome.Should().Be(BacklogHealthOutcome.NotEnoughHistory);
            result[check].Grade.Should().BeNull();
        }
        result.AgingWipDays.Should().BeNull();
        result.OversizedStoryPoints.Should().BeNull();
    }

    [Theory]
    [InlineData(12, HealthStatus.Healthy)]
    [InlineData(13, HealthStatus.AtRisk)]
    [InlineData(15, HealthStatus.AtRisk)]
    [InlineData(16, HealthStatus.Unhealthy)]
    public void Assess_NetFlow_GradesItemsCreatedPerItemCompleted(int itemsCreated, HealthStatus expected)
    {
        // Arrange
        var backlog = Items(5);

        // Act
        var result = Assess(backlog, History(itemsCreated: itemsCreated));

        // Assert
        var netFlow = result[BacklogHealthCheck.NetFlow];
        netFlow.Grade.Should().Be(expected);
        netFlow.Value.Should().BeApproximately(itemsCreated / 10.0, 0.0001);
    }

    [Fact]
    public void Assess_NetFlow_WorkCreatedButNoneCompleted_IsUnhealthy()
    {
        // Arrange
        var backlog = Items(5);

        // Act
        var result = Assess(backlog, History(completions: 0, itemsCreated: 3));

        // Assert
        var netFlow = result[BacklogHealthCheck.NetFlow];
        netFlow.Grade.Should().Be(HealthStatus.Unhealthy);
        netFlow.Value.Should().BeNull();
    }

    [Fact]
    public void Assess_NetFlow_NothingCreatedOrCompleted_IsNotApplicable()
    {
        // Arrange
        var backlog = Items(5);

        // Act
        var result = Assess(backlog, History(completions: 0, itemsCreated: 0));

        // Assert
        result[BacklogHealthCheck.NetFlow].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
    }

    [Theory]
    [InlineData(6, HealthStatus.Healthy)]
    [InlineData(8, HealthStatus.AtRisk)]
    [InlineData(10, HealthStatus.AtRisk)]
    [InlineData(11, HealthStatus.Unhealthy)]
    public void Assess_WipLoad_GradesActiveItemsPerMember(int activeItems, HealthStatus expected)
    {
        // Arrange
        var backlog = Items(activeItems, WorkStatusCategory.Active);

        // Act
        var result = Assess(backlog, memberCount: 5);

        // Assert
        var wipLoad = result[BacklogHealthCheck.WipLoad];
        wipLoad.Grade.Should().Be(expected);
        wipLoad.Value.Should().BeApproximately(activeItems / 5.0, 0.0001);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Assess_WipLoad_NoMembers_IsNotApplicable(int? memberCount)
    {
        // Arrange
        var backlog = Items(3, WorkStatusCategory.Active);

        // Act
        var result = Assess(backlog, memberCount: memberCount);

        // Assert
        result[BacklogHealthCheck.WipLoad].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
    }

    [Theory]
    [InlineData(0, HealthStatus.Healthy)]
    [InlineData(1, HealthStatus.AtRisk)]
    [InlineData(2, HealthStatus.AtRisk)]
    [InlineData(3, HealthStatus.Unhealthy)]
    public void Assess_ItemChecks_GradeByPercentFlagged(int staleItems, HealthStatus expected)
    {
        // Arrange
        var backlog = Items(10);
        for (var i = 0; i < staleItems; i++)
            backlog[i] = backlog[i] with { LastModified = _now - Duration.FromDays(200) };

        // Act
        var result = Assess(backlog);

        // Assert
        var stale = result[BacklogHealthCheck.Stale];
        stale.Grade.Should().Be(expected);
        stale.Flagged.Should().Be(staleItems);
        stale.InScope.Should().Be(10);
        stale.Value.Should().BeApproximately(staleItems * 10.0, 0.0001);
    }

    [Fact]
    public void Assess_Stale_FlagsItemsAtTheThreshold()
    {
        // Arrange
        var atThreshold = Item(1) with { LastModified = _now - Duration.FromDays(90) };
        var justUnder = Item(2) with { LastModified = _now - Duration.FromDays(90) + Duration.FromMinutes(1) };

        // Act
        var result = Assess([atThreshold, justUnder]);

        // Assert
        result.ItemFlags[atThreshold.Id].Should().Contain(BacklogHealthCheck.Stale);
        result.ItemFlags[justUnder.Id].Should().NotContain(BacklogHealthCheck.Stale);
    }

    [Fact]
    public void Assess_OldProposed_LooksOnlyAtProposedItems()
    {
        // Arrange
        var created = _now - Duration.FromDays(365);
        var proposed = Item(1) with { Created = created };
        var active = Item(2, WorkStatusCategory.Active) with { Created = created };

        // Act
        var result = Assess([proposed, active]);

        // Assert
        result[BacklogHealthCheck.OldProposed].InScope.Should().Be(1);
        result.ItemFlags[proposed.Id].Should().Contain(BacklogHealthCheck.OldProposed);
        result.ItemFlags[active.Id].Should().NotContain(BacklogHealthCheck.OldProposed);
    }

    [Fact]
    public void Assess_AgingWip_FlagsActiveItemsBeyondTheCycleTimePercentile()
    {
        // Arrange
        var history = History([.. Enumerable.Range(1, 20).Select(days => Completion(cycleTimeDays: days))]);
        var aging = Item(1, WorkStatusCategory.Active) with { Activated = _now - Duration.FromDays(18) };
        var onTrack = Item(2, WorkStatusCategory.Active) with { Activated = _now - Duration.FromDays(17) };
        var neverActivated = Item(3, WorkStatusCategory.Active) with { Activated = null };

        // Act
        var result = Assess([aging, onTrack, neverActivated], history);

        // Assert
        result.AgingWipDays.Should().BeApproximately(17, 0.0001);
        result.ItemFlags[aging.Id].Should().Contain(BacklogHealthCheck.AgingWip);
        result.ItemFlags[onTrack.Id].Should().NotContain(BacklogHealthCheck.AgingWip);
        result[BacklogHealthCheck.AgingWip].InScope.Should().Be(2);
    }

    [Fact]
    public void Assess_AgingWip_IgnoresCompletionsWithoutACycleTime()
    {
        // Arrange
        var completions = Enumerable.Range(0, 20)
            .Select(i => i < 11 ? new BacklogHealthCompletion(null, _now, 3) : Completion())
            .ToList();

        // Act
        var result = Assess(Items(3, WorkStatusCategory.Active), History(completions));

        // Assert
        result[BacklogHealthCheck.AgingWip].Outcome.Should().Be(BacklogHealthOutcome.NotEnoughHistory);
        result[BacklogHealthCheck.Runway].Outcome.Should().Be(BacklogHealthOutcome.Assessed);
    }

    [Fact]
    public void Assess_ReadinessWindow_IsSizedFromThroughput()
    {
        // Arrange
        var backlog = Items(10).Select(i => i with { StoryPoints = null }).ToList();

        // Act
        var result = Assess(backlog);

        // Assert
        result.ReadinessWindowItems.Should().Be(4);
        var missing = result[BacklogHealthCheck.MissingStoryPoints];
        missing.InScope.Should().Be(4);
        missing.Flagged.Should().Be(4);
        backlog.Where(i => i.Rank > 4).Should().AllSatisfy(i =>
            result.ItemFlags[i.Id].Should().NotContain(BacklogHealthCheck.MissingStoryPoints));
    }

    [Fact]
    public void Assess_ReadinessWindow_WithoutHistory_UsesTheFallbackSize()
    {
        // Arrange
        var backlog = Items(30);

        // Act
        var result = Assess(backlog, History(completions: 0));

        // Assert
        result.ReadinessWindowItems.Should().Be(BacklogHealthThresholds.Default.ReadinessFallbackItems);
    }

    [Fact]
    public void Assess_ReadinessWindow_FollowsRankNotInputOrder()
    {
        // Arrange
        var backlog = Items(10);
        var top = backlog[0] with { HasParent = false };
        var bottom = backlog[9] with { HasParent = false };
        IReadOnlyCollection<BacklogHealthItem> shuffled = [bottom, .. backlog.Skip(1).Take(8), top];

        // Act
        var result = Assess(shuffled);

        // Assert
        result.ItemFlags[top.Id].Should().Contain(BacklogHealthCheck.NoParent);
        result.ItemFlags[bottom.Id].Should().NotContain(BacklogHealthCheck.NoParent);
    }

    [Fact]
    public void Assess_Oversized_FlagsEstimatesAboveTheStoryPointPercentile()
    {
        // Arrange
        var history = History([.. Enumerable.Range(1, 20).Select(points => Completion(storyPoints: points))]);
        var oversized = Item(1) with { StoryPoints = 18 };
        var fits = Item(2) with { StoryPoints = 17 };
        var unestimated = Item(3) with { StoryPoints = null };

        // Act
        var result = Assess([oversized, fits, unestimated], history);

        // Assert
        result.OversizedStoryPoints.Should().Be(17);
        result.ItemFlags[oversized.Id].Should().Contain(BacklogHealthCheck.Oversized);
        result.ItemFlags[fits.Id].Should().NotContain(BacklogHealthCheck.Oversized);
        result[BacklogHealthCheck.Oversized].InScope.Should().Be(2);
    }

    [Fact]
    public void Assess_NoProject_TeamNotUsingProjects_IsNotApplicable()
    {
        // Arrange
        var backlog = Items(3).Select(i => i with { HasProject = false }).ToList();

        // Act
        var result = Assess(backlog, usesProjects: false);

        // Assert
        result[BacklogHealthCheck.NoProject].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
        backlog.Should().AllSatisfy(i => result.ItemFlags[i.Id].Should().NotContain(BacklogHealthCheck.NoProject));
    }

    [Fact]
    public void Assess_UnassignedActive_LooksOnlyAtActiveItems()
    {
        // Arrange
        var active = Item(1, WorkStatusCategory.Active) with { IsAssigned = false };
        var proposed = Item(2) with { IsAssigned = false };

        // Act
        var result = Assess([active, proposed]);

        // Assert
        result[BacklogHealthCheck.UnassignedActive].InScope.Should().Be(1);
        result.ItemFlags[active.Id].Should().Contain(BacklogHealthCheck.UnassignedActive);
        result.ItemFlags[proposed.Id].Should().NotContain(BacklogHealthCheck.UnassignedActive);
    }

    [Fact]
    public void Assess_CarryOver_FlagsItemsInACompletedSprint()
    {
        // Arrange
        var carriedOver = Item(1) with { SprintState = IterationState.Completed };
        var current = Item(2) with { SprintState = IterationState.Active };
        var unplanned = Item(3);

        // Act
        var result = Assess([carriedOver, current, unplanned]);

        // Assert
        result[BacklogHealthCheck.CarryOver].InScope.Should().Be(2);
        result.ItemFlags[carriedOver.Id].Should().Contain(BacklogHealthCheck.CarryOver);
        result.ItemFlags[current.Id].Should().NotContain(BacklogHealthCheck.CarryOver);
    }

    [Fact]
    public void Assess_CarryOver_NoItemsInSprints_IsNotApplicable()
    {
        // Act
        var result = Assess(Items(3));

        // Assert
        result[BacklogHealthCheck.CarryOver].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
    }

    [Fact]
    public void Assess_ClosedParent_LooksOnlyAtItemsWithAParent()
    {
        // Arrange
        var closedParent = Item(1) with { IsParentClosed = true };
        var openParent = Item(2);
        var noParent = Item(3) with { HasParent = false };

        // Act
        var result = Assess([closedParent, openParent, noParent]);

        // Assert
        result[BacklogHealthCheck.ClosedParent].InScope.Should().Be(2);
        result.ItemFlags[closedParent.Id].Should().Contain(BacklogHealthCheck.ClosedParent);
    }

    [Fact]
    public void Assess_RankInversion_AnyInversionIsUnhealthy()
    {
        // Arrange
        var backlog = Items(20);
        var predecessor = backlog[5];
        var successor = backlog[2] with { PredecessorIds = [predecessor.Id] };
        backlog[2] = successor;

        // Act
        var result = Assess(backlog);

        // Assert
        var inversion = result[BacklogHealthCheck.RankInversion];
        inversion.Grade.Should().Be(HealthStatus.Unhealthy);
        inversion.Flagged.Should().Be(1);
        result.ItemFlags[successor.Id].Should().Contain(BacklogHealthCheck.RankInversion);
        result.ItemFlags[predecessor.Id].Should().NotContain(BacklogHealthCheck.RankInversion);
    }

    [Fact]
    public void Assess_RankInversion_PredecessorRankedFirst_IsHealthy()
    {
        // Arrange
        var predecessor = Item(1);
        var successor = Item(2) with { PredecessorIds = [predecessor.Id] };

        // Act
        var result = Assess([predecessor, successor]);

        // Assert
        result[BacklogHealthCheck.RankInversion].Grade.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Assess_RankInversion_IgnoresPredecessorsNotOnTheBacklog()
    {
        // Arrange
        var successor = Item(1) with { PredecessorIds = [Guid.NewGuid()] };

        // Act
        var result = Assess([successor]);

        // Assert
        result[BacklogHealthCheck.RankInversion].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
    }

    [Fact]
    public void Assess_UsesTheThresholdsGiven()
    {
        // Arrange
        var thresholds = BacklogHealthThresholds.Default with { StaleDays = 5 };
        var item = Item(1) with { LastModified = _now - Duration.FromDays(6) };

        // Act
        var result = Assess([item], thresholds: thresholds);

        // Assert
        result.Thresholds.Should().Be(thresholds);
        result.ItemFlags[item.Id].Should().Contain(BacklogHealthCheck.Stale);
    }

    [Fact]
    public void Assess_EmptyBacklog_HasNoRunway()
    {
        // Act
        var result = Assess([]);

        // Assert
        result[BacklogHealthCheck.Runway].Grade.Should().Be(HealthStatus.Unhealthy);
        result[BacklogHealthCheck.Stale].Outcome.Should().Be(BacklogHealthOutcome.NotApplicable);
        result.ReadinessWindowItems.Should().Be(0);
    }

    [Theory]
    [InlineData(50, 5)]
    [InlineData(85, 9)]
    [InlineData(100, 10)]
    [InlineData(1, 1)]
    public void Percentile_UsesTheNearestRank(int percent, double expected)
    {
        // Arrange
        double[] values = [10, 1, 9, 2, 8, 3, 7, 4, 6, 5];

        // Act
        var result = BacklogHealthAssessor.Percentile(values, percent);

        // Assert
        result.Should().Be(expected);
    }
}
