using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public class BulkUpsertEmployeesCommandValidatorTests
{
    private readonly BulkUpsertEmployeesCommandValidator _validator = new();

    /// <summary>
    /// Both candidate keys are compared case-insensitively, matching the handler's lookup indexes
    /// and SQL Server's default (case-insensitive) collation. A case-sensitive check here would pass
    /// a payload the handler then collapses onto one employee — the second record silently
    /// overwriting the first — or which collides at SaveChanges and fails the whole batch.
    /// </summary>
    [Theory]
    [InlineData("a1b2", "A1B2", "shared@acme.example", "other@acme.example", "EmployeeNumber")]
    [InlineData("E-1", "E-2", "Shared@acme.example", "SHARED@ACME.EXAMPLE", "Email")]
    public void Validate_RejectsPayload_WhenCandidateKeysDifferOnlyByCasing(
        string firstNumber, string secondNumber, string firstEmail, string secondEmail, string field)
    {
        // Arrange
        var command = new BulkUpsertEmployeesCommand(
            [
                FakeExternalEmployee(firstNumber, firstEmail),
                FakeExternalEmployee(secondNumber, secondEmail),
            ],
            EmployeeMatchProperty.Email);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse($"{field} values differing only by casing are the same key");
        result.Errors.Should().Contain(e => e.ErrorMessage == $"{field} must be unique.");
    }

    /// <summary>
    /// The uniqueness rules must not reject a legitimate payload of distinct employees.
    /// </summary>
    [Fact]
    public void Validate_AcceptsPayload_WhenBothCandidateKeysAreDistinct()
    {
        // Arrange
        var command = new BulkUpsertEmployeesCommand(
            [
                FakeExternalEmployee("E-1", "ada@acme.example"),
                FakeExternalEmployee("E-2", "grace@acme.example"),
            ],
            EmployeeMatchProperty.Email);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static FakeExternalEmployeeRecord FakeExternalEmployee(string employeeNumber, string email) =>
        new()
        {
            EmployeeNumber = employeeNumber,
            Name = new PersonName("Test", null, employeeNumber),
            HireDate = null,
            Email = new EmailAddress(email),
            JobTitle = "Engineer",
            Department = "Engineering",
            OfficeLocation = "Remote",
            ManagerEmployeeNumber = null,
            IsActive = true,
            EmployeeType = "Employee",
        };

    private sealed record FakeExternalEmployeeRecord : IExternalEmployee
    {
        public required string EmployeeNumber { get; init; }
        public required PersonName Name { get; init; }
        public required Instant? HireDate { get; init; }
        public required EmailAddress Email { get; init; }
        public required string? JobTitle { get; init; }
        public required string? Department { get; init; }
        public required string? OfficeLocation { get; init; }
        public required string? ManagerEmployeeNumber { get; init; }
        public required bool IsActive { get; init; }
        public required string? EmployeeType { get; init; }
    }
}
