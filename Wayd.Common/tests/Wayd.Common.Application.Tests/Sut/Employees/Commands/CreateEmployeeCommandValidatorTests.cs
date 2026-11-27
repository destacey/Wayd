using FluentAssertions;
using FluentValidation.TestHelper;
using NodaTime;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Commands;

public sealed class CreateEmployeeCommandValidatorTests
{
    private readonly CreateEmployeeCommandValidator _validator = new(new FakeWaydDbContext());

    private static CreateEmployeeCommand Command(string employeeNumber = "ADV-1", string? employeeType = "Advisor", LocalDate? accessEndDate = null, bool withoutAccessEnd = false) =>
        new(new PersonName("Riley", null, "Moss"), employeeNumber, null, new EmailAddress("riley@acme.example"), null, null, null, null,
            employeeType, withoutAccessEnd ? null : accessEndDate ?? new LocalDate(2027, 6, 30));

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
    [InlineData(64, true)]
    [InlineData(65, false)]
    public async Task Validate_EmployeeNumber_IsLimitedToWhatAReferenceHolds(int length, bool valid)
    {
        // Act
        var result = await _validator.TestValidateAsync(Command(employeeNumber: new string('n', length)), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task Validate_EmployeeType_IsLimitedToItsColumn(int length, bool valid)
    {
        // Act
        var result = await _validator.TestValidateAsync(Command(employeeType: new string('t', length)), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().Be(valid);
    }
}
