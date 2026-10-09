using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintWorkingDaysTests
{
    private static readonly LocalDate Monday = new(2026, 9, 21);

    [Fact]
    public void Weight_IsOneOnAWorkingDayAndZeroOutsideTheWorkingWeek()
    {
        // Arrange
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, []);

        // Act
        var monday = sut.Weight(Monday);
        var sunday = sut.Weight(Monday.PlusDays(-1));

        // Assert
        monday.Should().Be(1);
        sunday.Should().Be(0);
    }

    [Fact]
    public void Weight_IsZeroOnADayOff()
    {
        // Arrange
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, [Monday]);

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0);
    }

    [Fact]
    public void EveryDay_WorksTheWeekend()
    {
        // Act
        var weight = SprintWorkingDays.EveryDay.Weight(Monday.PlusDays(-1));

        // Assert
        weight.Should().Be(1);
    }
}
