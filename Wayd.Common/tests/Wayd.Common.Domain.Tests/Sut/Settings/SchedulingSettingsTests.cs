using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Domain.Tests.Sut.Settings;

public sealed class SchedulingSettingsTests
{
    [Fact]
    public void Equals_ComparesWorkingDaysByValue()
    {
        // Arrange
        var stored = new SchedulingSettings { DefaultWorkingDays = [IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday] };
        var saved = new SchedulingSettings { DefaultWorkingDays = [IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday] };

        // Act
        var equal = stored == saved;

        // Assert
        equal.Should().BeTrue();
        stored.GetHashCode().Should().Be(saved.GetHashCode());
    }

    [Fact]
    public void Equals_IsFalseWhenTheWorkingDaysDiffer()
    {
        // Arrange
        var stored = new SchedulingSettings();
        var saved = new SchedulingSettings { DefaultWorkingDays = [IsoDayOfWeek.Monday] };

        // Act
        var equal = stored == saved;

        // Assert
        equal.Should().BeFalse();
    }

    [Fact]
    public void DefaultWorkingWeek_IsMondayToFridayByDefault()
    {
        // Act
        var week = new SchedulingSettings().DefaultWorkingWeek();

        // Assert
        week.Should().Be(WorkingWeek.MondayToFriday);
    }

    [Fact]
    public void DefaultWorkingWeek_FallsBackToMondayToFridayWhenTheStoredDaysAreInvalid()
    {
        // Arrange
        var settings = new SchedulingSettings { DefaultWorkingDays = [] };

        // Act
        var week = settings.DefaultWorkingWeek();

        // Assert
        week.Should().Be(WorkingWeek.MondayToFriday);
    }
}
