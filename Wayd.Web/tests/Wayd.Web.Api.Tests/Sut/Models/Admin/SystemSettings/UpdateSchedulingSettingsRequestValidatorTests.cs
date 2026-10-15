using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Admin.SystemSettings;

namespace Wayd.Web.Api.Tests.Sut.Models.Admin.SystemSettings;

public sealed class UpdateSchedulingSettingsRequestValidatorTests
{
    private readonly UpdateSchedulingSettingsRequestValidator _validator = new();

    private static UpdateSchedulingSettingsRequest Request(decimal? workingDayHours) => new()
    {
        DefaultTimeZone = "Etc/UTC",
        WorkingDayHours = workingDayHours,
    };

    [Fact]
    public void NoWorkingDayHours_Passes()
    {
        // Arrange
        var request = Request(workingDayHours: null);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.WorkingDayHours);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(24.5)]
    public void WorkingDayHours_OutsideOneToTwentyFour_Fails(double workingDayHours)
    {
        // Arrange
        var request = Request(workingDayHours: (decimal)workingDayHours);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WorkingDayHours);
    }
}
