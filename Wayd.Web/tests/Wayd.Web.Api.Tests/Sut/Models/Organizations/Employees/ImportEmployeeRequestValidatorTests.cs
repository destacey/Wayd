using FluentAssertions;
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

    [Fact]
    public void AdditionalEmails_SemicolonSeparated_Passes()
    {
        // Arrange
        var request = ValidRequest();
        request.AdditionalEmails = "ada@acme-legacy.example;a.lovelace@acme-legacy.example";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AdditionalEmails_Absent_Passes()
    {
        // Arrange — the column is optional, so existing files without it keep working.
        var request = ValidRequest();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AdditionalEmails_Malformed_FailsValidation()
    {
        // Arrange — same reasoning as the primary Email: the EmailAddress cast throws on a bad value.
        var request = ValidRequest();
        request.AdditionalEmails = "ada@acme-legacy.example;not-an-email";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(e => e.AdditionalEmails);
    }

    [Fact]
    public void AdditionalEmails_RepeatingThePrimary_FailsValidation()
    {
        // Arrange — Email is already recorded as the primary; repeating it would collide on the
        // EmployeeEmails unique index.
        var request = ValidRequest();
        request.AdditionalEmails = "ada.lovelace@acme.example";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(e => e.AdditionalEmails);
    }

    [Fact]
    public void AdditionalEmails_OverLongAddress_FailsValidation()
    {
        // Arrange
        var request = ValidRequest();
        request.AdditionalEmails = new string('x', 250) + "@acme-legacy.example";

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(e => e.AdditionalEmails);
    }

    [Fact]
    public void ToImportEmployeeDto_SplitsAdditionalEmails()
    {
        // Arrange
        var request = ValidRequest();
        request.AdditionalEmails = " ada@acme-legacy.example ; a.lovelace@acme-legacy.example ";

        // Act
        var dto = request.ToImportEmployeeDto();

        // Assert — CsvList trims each entry.
        dto.AdditionalEmails!.Select(e => e.Value).Should().BeEquivalentTo(
            ["ada@acme-legacy.example", "a.lovelace@acme-legacy.example"]);
    }
}
