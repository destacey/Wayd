using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Domain.Tests.Sut.Models.Organizations;

public sealed class WorkingWeekTests
{
    [Fact]
    public void Create_OrdersTheDaysFromMondayAndDropsRepeats()
    {
        // Arrange
        IsoDayOfWeek[] days = [IsoDayOfWeek.Sunday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday];

        // Act
        var result = WorkingWeek.Create(days);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Days.Should().Equal(IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Sunday);
    }

    [Fact]
    public void Create_WithNoDays_Fails()
    {
        // Act
        var result = WorkingWeek.Create([]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNull_Fails()
    {
        // Act
        var result = WorkingWeek.Create(null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(IsoDayOfWeek.None)]
    [InlineData((IsoDayOfWeek)8)]
    public void Create_WithSomethingThatIsNotADayOfTheWeek_Fails(IsoDayOfWeek day)
    {
        // Act
        var result = WorkingWeek.Create([IsoDayOfWeek.Monday, day]);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Includes_IsTrueOnlyOnTheWorkingDays()
    {
        // Arrange
        var sut = WorkingWeek.MondayToFriday;
        var friday = new LocalDate(2026, 10, 9);

        // Act
        var includesFriday = sut.Includes(friday);
        var includesSaturday = sut.Includes(friday.PlusDays(1));

        // Assert
        includesFriday.Should().BeTrue();
        includesSaturday.Should().BeFalse();
    }

    [Fact]
    public void Parse_ReadsBackWhatToStringWrote()
    {
        // Arrange
        var week = WorkingWeek.Create([IsoDayOfWeek.Sunday, IsoDayOfWeek.Monday, IsoDayOfWeek.Tuesday]).Value;

        // Act
        var text = week.ToString();
        var parsed = WorkingWeek.Parse(text);

        // Assert
        text.Should().Be("Monday,Tuesday,Sunday");
        parsed.Should().Be(week);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Monday,Funday")]
    public void Parse_WithSomethingThatIsNotAWorkingWeek_Throws(string value)
    {
        // Act
        var act = () => WorkingWeek.Parse(value);

        // Assert
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Equality_ComparesTheDays()
    {
        // Arrange
        var sameDays = WorkingWeek.Create([IsoDayOfWeek.Friday, IsoDayOfWeek.Thursday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Tuesday, IsoDayOfWeek.Monday]).Value;
        var otherDays = WorkingWeek.Create([IsoDayOfWeek.Monday]).Value;

        // Act
        var equalToSame = sameDays == WorkingWeek.MondayToFriday;
        var equalToOther = otherDays == WorkingWeek.MondayToFriday;

        // Assert
        equalToSame.Should().BeTrue();
        sameDays.GetHashCode().Should().Be(WorkingWeek.MondayToFriday.GetHashCode());
        equalToOther.Should().BeFalse();
    }
}
