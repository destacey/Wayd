using FluentAssertions;
using Wayd.Common.Application.SystemSettings.Scheduling;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.Scheduling;

public class SchedulingSettingsValidatorTests
{
    private readonly SchedulingSettingsValidator _sut = new();

    [Fact]
    public void Validate_AcceptsTheDefaults()
    {
        // Act
        var result = _sut.Validate(new SchedulingSettings());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("America/Chicago")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Kolkata")]
    public void Validate_AcceptsAnIanaTimeZone(string timeZone)
    {
        // Act
        var result = _sut.Validate(new SchedulingSettings { DefaultTimeZone = timeZone });

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Central Standard Time")]
    [InlineData("america/chicago")]
    public void Validate_RejectsAnythingElse(string timeZone)
    {
        // Act
        var result = _sut.Validate(new SchedulingSettings { DefaultTimeZone = timeZone });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SchedulingSettings.DefaultTimeZone));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(SchedulingSettingsValidator.MaxCommitmentGraceDays, true)]
    [InlineData(-1, false)]
    [InlineData(SchedulingSettingsValidator.MaxCommitmentGraceDays + 1, false)]
    public void Validate_BoundsTheGraceDays(int graceDays, bool isValid)
    {
        // Act
        var result = _sut.Validate(new SchedulingSettings { DefaultCommitmentGraceDays = graceDays });

        // Assert
        result.IsValid.Should().Be(isValid);
    }
}
