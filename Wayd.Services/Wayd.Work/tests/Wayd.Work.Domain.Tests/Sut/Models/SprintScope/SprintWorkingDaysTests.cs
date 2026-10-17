using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
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
    public void Weight_OnADayWithAvailability_IsTheShareOfTheTeamThatIsThere()
    {
        // Arrange
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, [], [], Availability((Monday, 0.625)));

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0.625);
    }

    [Fact]
    public void Weight_OnADayWithAvailability_DoesNotTakeTheTeamsHolidayAsWell()
    {
        // Arrange — the availability already leaves out the holidays each person takes
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, holidays: [Monday], teamDaysOff: [], Availability((Monday, 0.375)));

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0.375);
    }

    [Fact]
    public void Weight_OnATeamDayOff_IsZeroWhateverTheAvailability()
    {
        // Arrange
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, holidays: [], teamDaysOff: [new(Monday)], Availability((Monday, 0.875)));

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0);
    }

    [Fact]
    public void Weight_OnAPartTeamDayOff_TakesItsShareOfTheAvailability()
    {
        // Arrange — a half-day offsite on a day three quarters of the team is there
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, holidays: [], teamDaysOff: [new(Monday, 50)], Availability((Monday, 0.75)));

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0.375);
    }

    [Fact]
    public void Weight_OnAPartTeamDayOff_ReadAsAWholeTeam_IsTheShareWorked()
    {
        // Arrange
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, [], [new(Monday, 25)]);

        // Act
        var weight = sut.Weight(Monday);

        // Assert
        weight.Should().Be(0.75);
    }

    [Fact]
    public void Weight_OutsideTheWorkingWeek_IsZeroWhateverTheAvailability()
    {
        // Arrange
        var sunday = Monday.PlusDays(-1);
        var sut = new SprintWorkingDays(WorkingWeek.MondayToFriday, [], [], Availability((sunday, 1)));

        // Act
        var weight = sut.Weight(sunday);

        // Assert
        weight.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0)] // Monday: a holiday in the team's calendar
    [InlineData(1, 0)] // Tuesday: a team day off
    [InlineData(2, 1)] // Wednesday: worked
    [InlineData(4, 0.5)] // Friday: a half team day off
    [InlineData(5, 0)] // Saturday: outside the working week
    public void Weight_OnADayWithNoAvailability_IsReadAsAWholeTeam(int daysAfterMonday, double expected)
    {
        // Arrange — only Thursday has anyone allocated
        var sut = new SprintWorkingDays(
            WorkingWeek.MondayToFriday,
            holidays: [Monday],
            teamDaysOff: [new(Monday.PlusDays(1)), new(Monday.PlusDays(4), 50)],
            Availability((Monday.PlusDays(3), 0.5)));

        // Act
        var weight = sut.Weight(Monday.PlusDays(daysAfterMonday));

        // Assert
        weight.Should().Be(expected);
    }

    [Fact]
    public void EveryDay_WorksTheWeekend()
    {
        // Act
        var weight = SprintWorkingDays.EveryDay.Weight(Monday.PlusDays(-1));

        // Assert
        weight.Should().Be(1);
    }

    private static Dictionary<LocalDate, double> Availability(params (LocalDate Day, double Share)[] days) =>
        days.ToDictionary(d => d.Day, d => d.Share);
}
