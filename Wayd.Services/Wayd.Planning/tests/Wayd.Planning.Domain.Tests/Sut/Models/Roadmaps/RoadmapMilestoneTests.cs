using Wayd.Common.Models;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Planning.Domain.Tests.Models;
using Wayd.Tests.Shared;
using NodaTime.Extensions;
using NodaTime.Testing;

namespace Wayd.Planning.Domain.Tests.Sut.Models.Roadmaps;

public class RoadmapMilestoneTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly RoadmapActivityFaker _activityFaker;
    private readonly RoadmapMilestoneFaker _milestoneFaker;

    public RoadmapMilestoneTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _activityFaker = new RoadmapActivityFaker(Guid.NewGuid(), _dateTimeProvider.Today);
        _milestoneFaker = new RoadmapMilestoneFaker(localDate: _dateTimeProvider.Today);
    }

    private RoadmapActivity GenerateActivity(LocalDateRange dateRange) =>
        _activityFaker.WithDateRange(dateRange).Generate();

    private RoadmapMilestone CreateChildMilestone(RoadmapActivity parent, LocalDate date)
    {
        var milestone = _milestoneFaker.WithDate(date).Generate();
        return parent.CreateChildMilestone(new TestUpsertRoadmapMilestone(milestone));
    }

    private LocalDateRange Range(int startOffsetDays, int endOffsetDays) =>
        new(_dateTimeProvider.Today.PlusDays(startOffsetDays), _dateTimeProvider.Today.PlusDays(endOffsetDays));

    [Fact]
    public void Create_ShouldSetType()
    {
        // Arrange
        var milestone = _milestoneFaker.Generate();

        // Act
        var result = RoadmapMilestone.Create(milestone.RoadmapId, null, new TestUpsertRoadmapMilestone(milestone));

        // Assert
        result.Type.Should().Be(RoadmapItemType.Milestone);
        result.ParentId.Should().BeNull();
    }

    [Fact]
    public void UpdateDate_DateAfterParent_ShouldGrowParentEnd()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var milestone = CreateChildMilestone(parent, _dateTimeProvider.Today.PlusDays(5));

        // Act
        var result = milestone.UpdateDate(new TestUpsertRoadmapMilestoneDate(_dateTimeProvider.Today.PlusDays(25)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        milestone.Date.Should().Be(_dateTimeProvider.Today.PlusDays(25));
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(25));
    }

    [Fact]
    public void UpdateDate_DateBeforeParent_ShouldGrowParentStart()
    {
        // Arrange
        var parent = GenerateActivity(Range(10, 20));
        var milestone = CreateChildMilestone(parent, _dateTimeProvider.Today.PlusDays(15));

        // Act
        var result = milestone.UpdateDate(new TestUpsertRoadmapMilestoneDate(_dateTimeProvider.Today.PlusDays(2)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Start.Should().Be(_dateTimeProvider.Today.PlusDays(2));
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(20));
    }

    [Fact]
    public void UpdateDate_DateInsideParent_ShouldLeaveParentUnchanged()
    {
        // Arrange
        var parentRange = Range(0, 30);
        var parent = GenerateActivity(parentRange);
        var milestone = CreateChildMilestone(parent, _dateTimeProvider.Today.PlusDays(5));

        // Act
        var result = milestone.UpdateDate(new TestUpsertRoadmapMilestoneDate(_dateTimeProvider.Today.PlusDays(10)));

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.Should().Be(parentRange);
    }

    [Fact]
    public void UpdateDate_WhenNoParent_ShouldUpdateDate()
    {
        // Arrange
        var milestone = _milestoneFaker.WithDate(_dateTimeProvider.Today).Generate();
        var newDate = _dateTimeProvider.Today.PlusDays(7);

        // Act
        var result = milestone.UpdateDate(new TestUpsertRoadmapMilestoneDate(newDate));

        // Assert
        result.IsSuccess.Should().BeTrue();
        milestone.Date.Should().Be(newDate);
    }

    [Fact]
    public void Update_DateOutsideParent_ShouldGrowParent()
    {
        // Arrange
        var parent = GenerateActivity(Range(0, 10));
        var milestone = CreateChildMilestone(parent, _dateTimeProvider.Today.PlusDays(5));

        var update = new TestUpsertRoadmapMilestone(milestone)
        {
            ParentId = parent.Id,
            Date = _dateTimeProvider.Today.PlusDays(40)
        };

        // Act
        var result = milestone.Update(update, parent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        parent.DateRange.End.Should().Be(_dateTimeProvider.Today.PlusDays(40));
    }
}
