using FluentValidation.TestHelper;
using Wayd.Web.Api.Models.Organizations.Employees;

namespace Wayd.Web.Api.Tests.Sut.Models.Organizations.Employees;

public sealed class ImportEmployeeRequestValidatorTests
{
    private readonly ImportEmployeeRequestValidator _validator = new();

    private static ImportEmployeeRequest ValidRequest(string email = "ada.lovelace@acme.example") => new()
    {
        EmployeeNumber = "E-1001",
        FirstName = "Ada",
        LastName = "Lovelace",
        Email = email,
    };

    [Fact]
    public void ValidRequest_Passes()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.example")]
    [InlineData("spaces in@acme.example")]
    public void MalformedEmail_FailsValidation(string email)
    {
        // Arrange — an invalid email would otherwise throw when cast to EmailAddress in ToImportEmployeeDto,
        // surfacing as a 500 instead of a 422; the validator must reject it first.
        var request = ValidRequest(email);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(e => e.Email);
    }
}
