using FluentAssertions;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public class ImportEmployeesCommandValidatorTests
{
    private readonly ImportEmployeesCommandValidator _validator = new();

    private static ImportEmployeeDto Row(string employeeNumber, string email, string[]? additionalEmails = null) =>
        new(
            employeeNumber,
            FirstName: "Test",
            MiddleName: null,
            LastName: employeeNumber,
            Email: new EmailAddress(email),
            HireDate: null,
            JobTitle: "Engineer",
            Department: "Engineering",
            OfficeLocation: "Remote",
            ManagerNumber: null,
            AdditionalEmails: additionalEmails is null ? null : [.. additionalEmails.Select(e => new EmailAddress(e))]);

    [Fact]
    public void Validate_AcceptsDistinctEmployees()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "ada@acme.example", ["ada@acme-legacy.example"]),
            Row("E-1002", "grace@acme.example", ["grace@acme-legacy.example"]),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsDuplicateEmployeeNumbers()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "ada@acme.example"),
            Row("e-1001", "grace@acme.example"),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "EmployeeNumber must be unique.");
    }

    /// <summary>
    /// Employees.Email is uniquely indexed, so a repeat fails the batch at SaveChanges with a raw
    /// duplicate-key error naming neither the row nor the field. The validator has to catch it first.
    /// </summary>
    [Fact]
    public void Validate_RejectsDuplicatePrimaryEmails()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "shared@acme.example"),
            Row("E-1002", "SHARED@ACME.EXAMPLE"),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse("addresses differing only by casing are the same key");
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email must be unique, including AdditionalEmails.");
    }

    [Fact]
    public void Validate_RejectsTheSameAddressClaimedByTwoRowsAsAnAdditionalEmail()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "ada@acme.example", ["shared@acme-legacy.example"]),
            Row("E-1002", "grace@acme.example", ["shared@acme-legacy.example"]),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email must be unique, including AdditionalEmails.");
    }

    /// <summary>
    /// The cross-column case: one row's additional address is another row's primary. Both land in
    /// EmployeeEmails, whose unique index spans the whole table.
    /// </summary>
    [Fact]
    public void Validate_RejectsAnAdditionalEmailThatIsAnotherRowsPrimary()
    {
        // Arrange
        var command = new ImportEmployeesCommand(
        [
            Row("E-1001", "ada@acme.example"),
            Row("E-1002", "grace@acme.example", ["ada@acme.example"]),
        ]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email must be unique, including AdditionalEmails.");
    }

    [Fact]
    public void Validate_RejectsARowRepeatingItsOwnPrimaryAsAnAdditionalEmail()
    {
        // Arrange
        var command = new ImportEmployeesCommand([Row("E-1001", "ada@acme.example", ["ada@acme.example"])]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email must be unique, including AdditionalEmails.");
    }

    [Fact]
    public void Validate_RejectsAnEmptyBatch()
    {
        // Arrange
        var command = new ImportEmployeesCommand([]);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
