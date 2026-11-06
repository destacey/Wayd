using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Admin.SystemSettings;

namespace Wayd.Web.Api.Tests.Sut.Models.Admin.SystemSettings;

public sealed class UpdateGeneralSettingsRequestValidatorTests
{
    private readonly UpdateGeneralSettingsRequestValidator _validator = new();

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/Chicago")]
    public void IanaTimeZone_Passes(string timeZone)
    {
        // Arrange
        var request = new UpdateGeneralSettingsRequest { TimeZone = timeZone };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.TimeZone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Central Standard Time")]
    public void AnythingElse_Fails(string timeZone)
    {
        // Arrange
        var request = new UpdateGeneralSettingsRequest { TimeZone = timeZone };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TimeZone);
    }
}
