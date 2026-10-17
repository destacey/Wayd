using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TeamDayTests
{
    private static readonly LocalDate Monday = new(2027, 2, 1);
    private static readonly TeamDay[] Week = [.. Enumerable.Range(0, 5).Select(i => new TeamDay(Monday.PlusDays(i), null))];

    [Fact]
    public void WithDaysOff_TakesEachDaysShareOffAndKeepsAFullDayAtZero()
    {
        // Act
        var days = TeamDay.WithDaysOff(Week, [new TeamDayOff(Monday), new TeamDayOff(Monday.PlusDays(2), 25)]);

        // Assert
        days.Select(d => (d.Date, d.ShareWorked)).Should().Equal(
            (Monday, 0m),
            (Monday.PlusDays(1), 1m),
            (Monday.PlusDays(2), 0.75m),
            (Monday.PlusDays(3), 1m),
            (Monday.PlusDays(4), 1m));
    }

    [Fact]
    public void WithDaysOff_ForADayListedTwice_TakesTheLargerShareOffOnce()
    {
        // Arrange — the same day from a sprint's period and from the team's own days off
        TeamDayOff[] daysOff = [new(Monday, 50), new(Monday, 25), new(Monday, 50)];

        // Act
        var days = TeamDay.WithDaysOff(Week, daysOff);

        // Assert
        days[0].ShareWorked.Should().Be(0.5m);
    }

    [Fact]
    public void WithDaysOff_WithNone_GivesTheDaysUnchanged()
    {
        // Act
        var days = TeamDay.WithDaysOff(Week, []);

        // Assert
        days.Should().Equal(Week);
    }
}
