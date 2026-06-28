using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Planning.Domain.Tests.Models;
using Wayd.Tests.Shared;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Planning.Domain.Tests.Sut.Models.Roadmaps;

public class RoadmapTimeboxTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly RoadmapActivityFaker _activityFaker;
    private readonly RoadmapTimeboxFaker _timeboxFaker;

    public RoadmapTimeboxTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _activityFaker = new RoadmapActivityFaker(Guid.NewGuid(), _dateTimeProvider.Today);
        _timeboxFaker = new RoadmapTimeboxFaker(localDate: _dateTimeProvider.Today);
    }

    private RoadmapActivity GenerateActivity(LocalDateRange dateRange) =>
        _activityFaker.WithDateRange(dateRange).Generate();

    private RoadmapTimebox CreateChildTimebox(RoadmapActivity parent, LocalDateRange dateRange)
    {
        var timebox = _timeboxFaker.WithDateRange(dateRange).Generate();
        return parent.CreateChildTimebox(new TestUpsertRoadmapTimebox(timebox));
    }

    private LocalDateRange Range(int startOffsetDays, int endOffsetDays) =>
        new(_dateTimeProvider.Today.PlusDays(startOffsetDays), _dateTimeProvider.Today.PlusDays(endOffsetDays));

    [Fact]
    public void Create_ShouldSetType()
    {
        // Arrange
        var timebox = _timeboxFaker.Generate();

        // Act
        var result = RoadmapTimebox.Create(timebox.RoadmapId, null, new TestUpsertRoadmapTimebox(timebox));

        // Assert
        result.Type.Should().Be(RoadmapItemType.Timebox);
        result.ParentId.Should().BeNull();
    }

    [Fact]
    public void UpdateDateRange_RangeEndsAfterParent_ShouldGrowParentEnd()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var timebox = CreateChildTimebox(parent, Range(2, 8));

        // Act
        var result = timebox.UpdateDateRange(new TestUpsertRoadmapTimeboxDateRange(Range(2, 25)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        timebox.DateRange.Should().Be(Range(2, 25));
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(25));
    }

    [Fact]
    public void UpdateDateRange_RangeStartsBeforeParent_ShouldGrowParentStart()
    {
        // Arrange
        var parent = GenerateActivity(Range(10, 20));
        var timebox = CreateChildTimebox(parent, Range(12, 18));

        // Act
        var result = timebox.UpdateDateRange(new TestUpsertRoadmapTimeboxDateRange(Range(2, 18)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today.PlusDays(2));
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void UpdateDateRange_RangeInsideParent_ShouldLeaveParentUnchanged()
    {
        // Arrange
        var parentRange = Range(0, 30);
        var parent = GenerateActivity(parentRange);
        var timebox = CreateChildTimebox(parent, Range(5, 10));

        // Act
        var result = timebox.UpdateDateRange(new TestUpsertRoadmapTimeboxDateRange(Range(8, 12)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(parentRange);
    }

    [Fact]
    public void UpdateDateRange_WhenNoParent_ShouldUpdateRange()
    {
        // Arrange
        var timebox = _timeboxFaker.WithDateRange(Range(0, 10)).Generate();
        var newRange = Range(3, 8);

        // Act
        var result = timebox.UpdateDateRange(new TestUpsertRoadmapTimeboxDateRange(newRange));

        // Assert
        result.IsSuccess.Should().BeTrue();
        timebox.DateRange.Should().Be(newRange);
    }

    [Fact]
    public void Update_RangeOutsideParent_ShouldGrowParent()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var timebox = CreateChildTimebox(parent, Range(2, 8));

        var update = new TestUpsertRoadmapTimebox(timebox)
        {
            ParentId = parent.Id,
            DateRange = Range(2, 40)
        };

        // Act
        var result = timebox.Update(update, parent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
    }
}
