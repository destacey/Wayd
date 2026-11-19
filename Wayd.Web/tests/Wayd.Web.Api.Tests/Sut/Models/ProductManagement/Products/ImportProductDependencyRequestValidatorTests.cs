using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.ProductManagement.Products;

namespace Wayd.Web.Api.Tests.Sut.Models.ProductManagement.Products;

public sealed class ImportProductDependencyRequestValidatorTests
{
    private readonly ImportProductDependencyRequestValidator _validator = new();

    private static ImportProductDependencyRequest Request() => new()
    {
        ImportId = "dep-1",
        ProductId = Guid.NewGuid(),
        DependsOnProductId = Guid.NewGuid(),
        Strength = "Hard",
    };

    [Fact]
    public void AnEndBeforeItsStart_Fails()
    {
        // Arrange
        var request = Request();
        request.StartsOn = new DateOnly(2026, 3, 10);
        request.EndsOn = new DateOnly(2026, 3, 9);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.EndsOn)
            .WithErrorMessage("A dependency cannot end before it started.");
    }

    [Fact]
    public void AnEndOnItsStart_Passes()
    {
        // Arrange
        var request = Request();
        request.StartsOn = new DateOnly(2026, 3, 10);
        request.EndsOn = new DateOnly(2026, 3, 10);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AnEndWithABlankStart_IsLeftToTheImportCommand()
    {
        // Arrange
        var request = Request();
        request.EndsOn = new DateOnly(2000, 1, 1);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.EndsOn);
    }
}
