using System.Globalization;
using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Organizations.Employees;

namespace Wayd.Web.Api.Tests.Sut.Models.Organizations.Employees;

public sealed class ChangeEmployeeWorkingDayHoursRequestValidatorTests
{
    private readonly ChangeEmployeeWorkingDayHoursRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("4.25")]
    [InlineData("7.50")]
    [InlineData("24")]
    public void WorkingDayHours_WithinTheDayToHundredths_Passes(string? hours)
    {
        // Arrange
        var request = new ChangeEmployeeWorkingDayHoursRequest { WorkingDayHours = Parse(hours) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("24.25")]
    [InlineData("4.125")]
    public void WorkingDayHours_OutsideTheDayOrPastHundredths_FailsOnTheField(string hours)
    {
        // Arrange
        var request = new ChangeEmployeeWorkingDayHoursRequest { WorkingDayHours = Parse(hours) };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.WorkingDayHours);
    }

    private static decimal? Parse(string? hours) =>
        hours is null ? null : decimal.Parse(hours, CultureInfo.InvariantCulture);
}
