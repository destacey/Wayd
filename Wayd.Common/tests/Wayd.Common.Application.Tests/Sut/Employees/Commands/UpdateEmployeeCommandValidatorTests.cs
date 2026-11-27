using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class UpdateEmployeeCommandValidatorTests
{
    private readonly UpdateEmployeeCommandValidator _validator = new(new FakeWaydDbContext());

    private static UpdateEmployeeCommand Command(string employeeNumber = "ADV-1", string? employeeType = "Advisor", bool withoutAccessEnd = false) =>
        new(Guid.NewGuid(), new PersonName("Riley", null, "Moss"), employeeNumber, null, new EmailAddress("riley@acme.example"), null, null, null, null,
            employeeType, withoutAccessEnd ? null : new LocalDate(2027, 6, 30));

    [Fact]
    public async Task Validate_AWellFormedCommand_Passes()
    {
        // Act
        var result = await _validator.TestValidateAsync(Command(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithoutAnAccessEndDate_Fails()
    {
        // Act
        var result = await _validator.TestValidateAsync(Command(withoutAccessEnd: true), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.AccessEndDate);
    }

    [Theory]
    [InlineData(65, nameof(UpdateEmployeeCommand.EmployeeNumber))]
    [InlineData(129, nameof(UpdateEmployeeCommand.EmployeeType))]
    public async Task Validate_OverLongValue_Fails(int length, string field)
    {
        // Arrange
        var value = new string('x', length);
        var command = field == nameof(UpdateEmployeeCommand.EmployeeNumber) ? Command(employeeNumber: value) : Command(employeeType: value);

        // Act
        var result = await _validator.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == field);
    }
}
