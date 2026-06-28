using Wayd.Common.Models;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Planning.Domain.Tests.Models;
using Wayd.Tests.Shared;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Planning.Domain.Tests.Sut.Models.Roadmaps;

public class RoadmapActivityTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly RoadmapActivityFaker _activityFaker;
    private readonly RoadmapMilestoneFaker _milestoneFaker;
    private readonly RoadmapTimeboxFaker _timeboxFaker;

    public RoadmapActivityTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _activityFaker = new RoadmapActivityFaker(Guid.NewGuid(), _dateTimeProvider.Today);
        _milestoneFaker = new RoadmapMilestoneFaker(localDate: _dateTimeProvider.Today);
        _timeboxFaker = new RoadmapTimeboxFaker(localDate: _dateTimeProvider.Today);
    }

    private RoadmapActivity GenerateActivity(LocalDateRange dateRange) =>
        _activityFaker.WithDateRange(dateRange).Generate();

    private LocalDateRange Range(int startOffsetDays, int endOffsetDays) =>
        new(_dateTimeProvider.Today.PlusDays(startOffsetDays), _dateTimeProvider.Today.PlusDays(endOffsetDays));

    #region CreateChildActivity Rollup

    [Fact]
    public void CreateChildActivity_ChildEndsAfterParent_ShouldGrowParentEnd()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var child = GenerateActivity(Range(5, 20));

        // Act
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(child));

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void CreateChildActivity_ChildStartsBeforeParent_ShouldGrowParentStart()
    {
        // Arrange
        var parent = GenerateActivity(Range(10, 20));
        var child = GenerateActivity(Range(0, 15));

        // Act
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(child));

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void CreateChildActivity_ChildFullyInsideParent_ShouldLeaveParentUnchanged()
    {
        // Arrange
        var parentRange = Range(0, 30);
        var parent = GenerateActivity(parentRange);
        var child = GenerateActivity(Range(5, 10));

        // Act
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(child));

        // Assert
        parent.DateRange.Should().Be(parentRange);
    }

    [Fact]
    public void CreateChildActivity_ShouldLinkChildToParent()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var child = GenerateActivity(Range(0, 10));

        // Act
        var created = parent.CreateChildActivity(new TestUpsertRoadmapActivity(child));

        // Assert
        created.ParentId.Should().Be(parent.Id);
        created.Parent.Should().Be(parent);
    }

    #endregion CreateChildActivity Rollup

    #region CreateChildMilestone / CreateChildTimebox Rollup

    [Fact]
    public void CreateChildMilestone_DateAfterParent_ShouldGrowParentEnd()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var milestone = _milestoneFaker.WithDate(_dateTimeProvider.Today.PlusDays(25)).Generate();

        // Act
        parent.CreateChildMilestone(new TestUpsertRoadmapMilestone(milestone));

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(25));
    }

    [Fact]
    public void CreateChildMilestone_DateBeforeParent_ShouldGrowParentStart()
    {
        // Arrange
        var parent = GenerateActivity(Range(10, 20));
        var milestone = _milestoneFaker.WithDate(_dateTimeProvider.Today.PlusDays(2)).Generate();

        // Act
        parent.CreateChildMilestone(new TestUpsertRoadmapMilestone(milestone));

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today.PlusDays(2));
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void CreateChildTimebox_RangeOutsideParent_ShouldGrowParent()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var timebox = _timeboxFaker.WithDateRange(Range(8, 18)).Generate();

        // Act
        parent.CreateChildTimebox(new TestUpsertRoadmapTimebox(timebox));

        // Assert
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today);
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(18));
    }

    #endregion CreateChildMilestone / CreateChildTimebox Rollup

    #region UpdateDateRange Rollup

    [Fact]
    public void UpdateDateRange_NoChildren_ShouldApplyRangeUnchanged()
    {
        // Arrange
        var activity = GenerateActivity(Range(0, 10));
        var newRange = Range(2, 6);

        // Act
        var result = activity.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(newRange));

        // Assert
        result.IsSuccess.Should().BeTrue();
        activity.DateRange.Should().Be(newRange);
    }

    [Fact]
    public void UpdateDateRange_NarrowerThanChildren_ShouldFailAndNotChangeRange()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(0, 20))));
        // The child grew the parent to [0, 20] on creation.
        var rangeBefore = parent.DateRange;

        // Act - attempt to shrink the parent behind its child's end
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(0, 5)));

        // Assert - rejected, range untouched
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must contain all child items");
        parent.DateRange.Should().Be(rangeBefore);
    }

    [Fact]
    public void UpdateDateRange_WiderThanChildren_ShouldKeepWiderRange()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(2, 6))));
        var widerRange = Range(-5, 30);

        // Act
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(widerRange));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(widerRange);
    }

    [Fact]
    public void UpdateDateRange_ExactlyContainsChildren_ShouldSucceed()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 30));
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(5, 20))));
        var tightRange = Range(5, 20);

        // Act - shrink the parent to exactly the child's span (boundary is allowed)
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(tightRange));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(tightRange);
    }

    [Fact]
    public void UpdateDateRange_StartAfterChildStart_ShouldFail()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 30));
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(0, 10))));
        var rangeBefore = parent.DateRange;

        // Act - move the parent start ahead of the child's start
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(5, 30)));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must contain all child items");
        parent.DateRange.Should().Be(rangeBefore);
    }

    #endregion UpdateDateRange Rollup

    #region RecalculateDateRangeFromChildren

    [Fact]
    public void RecalculateDateRangeFromChildren_ShouldBubbleUpToGrandparent()
    {
        // Arrange
        var grandparent = GenerateActivity(Range(0, 10));
        var parent = grandparent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(0, 10))));
        var child = parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(0, 10))));

        // Act - grow the leaf child, then trigger a recalculate from its parent
        child.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(0, 40)));

        // Assert - growth bubbled all the way to the root
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
        grandparent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
    }

    [Fact]
    public void RecalculateDateRangeFromChildren_NoChildren_ShouldLeaveRangeUnchanged()
    {
        // Arrange
        var range = Range(0, 10);
        var activity = GenerateActivity(range);

        // Act
        activity.RecalculateDateRangeFromChildren();

        // Assert
        activity.DateRange.Should().Be(range);
    }

    #endregion RecalculateDateRangeFromChildren

    #region Subtree Shift

    [Fact]
    public void UpdateDateRange_PureShiftForward_ShouldMoveAllDescendantsBySameDelta()
    {
        // Arrange - parent [0,30] with an activity child [5,15] that itself has a grandchild [6,10]
        var parent = GenerateActivity(Range(0, 30));
        var child = parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(5, 15))));
        var grandchild = child.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(6, 10))));

        // Act - shift the parent forward by 7 days (same duration)
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(7, 37)));

        // Assert - every node moved by exactly 7 days
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(Range(7, 37));
        child.DateRange.Should().Be(Range(12, 22));
        grandchild.DateRange.Should().Be(Range(13, 17));
    }

    [Fact]
    public void UpdateDateRange_PureShiftBackward_ShouldMoveAllDescendantsBySameDelta()
    {
        // Arrange
        var parent = GenerateActivity(Range(10, 30));
        var child = parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(12, 20))));

        // Act - shift the parent back by 5 days
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(5, 25)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(Range(5, 25));
        child.DateRange.Should().Be(Range(7, 15));
    }

    [Fact]
    public void UpdateDateRange_PureShift_ShouldMoveTimeboxAndMilestoneChildren()
    {
        // Arrange - parent with a timebox and a milestone child
        var parent = GenerateActivity(Range(0, 30));
        var timebox = parent.CreateChildTimebox(
            new TestUpsertRoadmapTimebox(_timeboxFaker.WithDateRange(Range(2, 8)).Generate()));
        var milestone = parent.CreateChildMilestone(
            new TestUpsertRoadmapMilestone(_milestoneFaker.WithDate(_dateTimeProvider.Today.PlusDays(15)).Generate()));

        // Act - shift the parent forward by 10 days
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(10, 40)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        timebox.DateRange.Should().Be(Range(12, 18));
        milestone.Date.Should().Be(_dateTimeProvider.Today.PlusDays(25));
    }

    [Fact]
    public void UpdateDateRange_ResizeNotShift_ShouldNotMoveChildren()
    {
        // Arrange - parent [0,30] wide enough to resize without a uniform shift
        var parent = GenerateActivity(Range(0, 30));
        var child = parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(5, 15))));
        var childRangeBefore = child.DateRange;

        // Act - move only the end (start delta 0, end delta -5): a resize, not a shift
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(0, 25)));

        // Assert - the parent resized; the child did not move
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(Range(0, 25));
        child.DateRange.Should().Be(childRangeBefore);
    }

    [Fact]
    public void UpdateDateRange_PureShiftWithNoChildren_ShouldJustMoveActivity()
    {
        // Arrange - a leaf activity
        var activity = GenerateActivity(Range(0, 10));

        // Act - shift it forward (no children to move)
        var result = activity.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(7, 17)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        activity.DateRange.Should().Be(Range(7, 17));
    }

    [Fact]
    public void UpdateDateRange_PureShift_ShouldGrowAncestorToFitShiftedChild()
    {
        // Arrange - grandparent just barely contains the parent it owns
        var grandparent = GenerateActivity(Range(0, 30));
        var parent = grandparent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(5, 25))));
        parent.CreateChildActivity(new TestUpsertRoadmapActivity(GenerateActivity(Range(6, 20))));

        // Act - shift the parent forward by 10 days; its end (35) now exceeds the grandparent (30)
        var result = parent.UpdateDateRange(new TestUpsertRoadmapActivityDateRange(Range(15, 35)));

        // Assert - the grandparent grew to contain the shifted subtree
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(Range(15, 35));
        grandparent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(35));
    }

    #endregion Subtree Shift
}
