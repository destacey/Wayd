using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

namespace Wayd.Web.Api.Tests.Sut.Models.Ppm.StrategicInitiatives;

public sealed class UpdateStrategicInitiativeKpiRequestValidatorTests
{
    private readonly UpdateStrategicInitiativeKpiRequestValidator _validator = new();

    private static UpdateStrategicInitiativeKpiRequest Request(string? description = null, string? prefix = null, string? suffix = null) => new()
    {
        StrategicInitiativeId = Guid.NewGuid(),
        KpiId = Guid.NewGuid(),
        Name = "Net retention",
        TargetValue = 95,
        Description = description,
        Prefix = prefix,
        Suffix = suffix,
    };

    [Fact]
    public void NoDescriptionPrefixOrSuffix_Passes()
    {
        // Arrange
        var request = Request();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Description);
        result.ShouldNotHaveValidationErrorFor(r => r.Prefix);
        result.ShouldNotHaveValidationErrorFor(r => r.Suffix);
    }

    [Fact]
    public void Description_Over512Characters_Fails()
    {
        // Arrange
        var request = Request(description: new string('d', 513));

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Description);
    }

    [Fact]
    public void PrefixAndSuffix_Over8Characters_Fail()
    {
        // Arrange
        var request = Request(prefix: "123456789", suffix: "123456789");

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Prefix);
        result.ShouldHaveValidationErrorFor(r => r.Suffix);
    }
}
