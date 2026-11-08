using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class DependencyWorkItemInfoTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly DateTimeZone Tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];
    private static readonly LocalDate LastDay = new(2026, 9, 27);

    private static Instant InZone(DateTimeZone zone, LocalDate date, int hour) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(zone).ToInstant();

    [Fact]
    public void Create_WithNoSprint_HasNoPlan()
    {
        // Arrange
        var workItemId = Guid.NewGuid();

        // Act
        var info = DependencyWorkItemInfo.Create(workItemId, StatusCategory.Active);

        // Assert
        Assert.Equal(workItemId, info.WorkItemId);
        Assert.Equal(StatusCategory.Active, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithFutureSprint_PlansOnItsLastDay()
    {
        // Arrange
        var now = InZone(Chicago, LastDay.PlusDays(-5), 12);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, LastDay, Chicago, now);

        // Assert
        Assert.Equal(LastDay, info.PlannedOn);
    }

    [Fact]
    public void Create_DuringTheSprintsLastDayInItsZone_PlansOnThatDay()
    {
        // Arrange — 23:00 in Chicago on the last day is already the next day in UTC
        var now = InZone(Chicago, LastDay, 23);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, LastDay, Chicago, now);

        // Assert
        Assert.Equal(LastDay, info.PlannedOn);
    }

    [Fact]
    public void Create_AtTheEndOfTheSprintsLastDayInItsZone_HasNoPlan()
    {
        // Arrange — midnight after the last day in Chicago
        var now = InZone(Chicago, LastDay.PlusDays(1), 0);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, LastDay, Chicago, now);

        // Assert
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_AfterTheLastDayInTheSprintsZone_HasNoPlanEvenWhileItIsStillThatDayInUtc()
    {
        // Arrange — 05:00 on the day after in Tokyo is 20:00 UTC on the last day
        var now = InZone(Tokyo, LastDay.PlusDays(1), 5);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, LastDay, Tokyo, now);

        // Assert
        Assert.Null(info.PlannedOn);
    }

    [Theory]
    [InlineData(5, 30, true)]
    [InlineData(6, 0, false)]
    public void Create_WhenTheLastDayEndsDaylightSaving_EndsAtMidnightStandardTime(int utcHour, int utcMinute, bool stillPlanned)
    {
        // Arrange — Chicago leaves daylight saving on Nov 1 2026, so that day ends at 06:00 UTC, not 05:00
        var lastDay = new LocalDate(2026, 11, 1);
        var now = Instant.FromUtc(2026, 11, 2, utcHour, utcMinute);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, lastDay, Chicago, now);

        // Assert
        Assert.Equal(stillPlanned ? lastDay : (LocalDate?)null, info.PlannedOn);
    }

    [Theory]
    [InlineData(StatusCategory.Proposed)]
    [InlineData(StatusCategory.Active)]
    [InlineData(StatusCategory.Done)]
    [InlineData(StatusCategory.Removed)]
    public void Create_PreservesStatusCategory(StatusCategory statusCategory)
    {
        // Arrange
        var now = InZone(Chicago, LastDay, 12);

        // Act
        var info = DependencyWorkItemInfo.Create(Guid.NewGuid(), statusCategory, LastDay, Chicago, now);

        // Assert
        Assert.Equal(statusCategory, info.StatusCategory);
    }
}
