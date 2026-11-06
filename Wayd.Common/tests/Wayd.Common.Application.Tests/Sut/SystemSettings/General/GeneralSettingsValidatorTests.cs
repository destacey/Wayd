using FluentAssertions;
using Wayd.Common.Application.SystemSettings.General;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.General;

public class GeneralSettingsValidatorTests
{
    private readonly GeneralSettingsValidator _sut = new();

    [Fact]
    public void Validate_AcceptsTheDefaults()
    {
        // Act
        var result = _sut.Validate(new GeneralSettings());

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
        var result = _sut.Validate(new GeneralSettings { TimeZone = timeZone });

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
        var result = _sut.Validate(new GeneralSettings { TimeZone = timeZone });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GeneralSettings.TimeZone));
    }
}
