using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.Users;

namespace Wayd.Common.Application.Tests.Sut.Identity.Users;

public class UpdateUserCommandValidatorTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly UpdateUserCommandValidator _sut;

    public UpdateUserCommandValidatorTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockUserService.Setup(x => x.ExistsWithEmailAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
        _mockUserService.Setup(x => x.ExistsWithPhoneNumberAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(false);
        _mockUserService.Setup(x => x.ExistsWithEmployeeIdAsync(It.IsAny<Guid>(), It.IsAny<string?>())).ReturnsAsync(false);

        _sut = new UpdateUserCommandValidator(_mockUserService.Object);
    }

    private static UpdateUserCommand CreateValidCommand() => new()
    {
        Id = "user-1",
        FirstName = "John",
        LastName = "Doe",
        Email = "john.doe@acme.example",
    };

    [Fact]
    public async Task Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenEmployeeIsNotLinkedToAnotherUser()
    {
        // Arrange
        var command = CreateValidCommand();
        command.ManageEmployeeLink = true;
        command.EmployeeId = Guid.NewGuid();

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeId);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEmployeeIsAlreadyLinkedToAnotherUser()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var command = CreateValidCommand();
        command.ManageEmployeeLink = true;
        command.EmployeeId = employeeId;

        _mockUserService
            .Setup(x => x.ExistsWithEmployeeIdAsync(employeeId, command.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId)
            .WithErrorMessage("That employee is already linked to another user.");
    }

    [Fact]
    public async Task Validate_ShouldExcludeTheUserBeingEdited_WhenCheckingEmployeeUniqueness()
    {
        // Arrange — re-saving a user without changing their own existing link must not
        // report the user as a conflict with themselves.
        var employeeId = Guid.NewGuid();
        var command = CreateValidCommand();
        command.ManageEmployeeLink = true;
        command.EmployeeId = employeeId;

        // Act
        await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        _mockUserService.Verify(x => x.ExistsWithEmployeeIdAsync(employeeId, "user-1"), Times.Once);
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenClearingTheEmployeeLink()
    {
        // Arrange — clearing the link is always allowed; NULL never collides.
        var command = CreateValidCommand();
        command.ManageEmployeeLink = true;
        command.EmployeeId = null;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeId);
        _mockUserService.Verify(x => x.ExistsWithEmployeeIdAsync(It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Validate_ShouldNotCheckEmployeeUniqueness_WhenUpdateDoesNotManageTheLink()
    {
        // Arrange — the self-service profile edit carries no employee id and must not be
        // validated (or rejected) on a link it is not administering.
        var command = CreateValidCommand();
        command.ManageEmployeeLink = false;
        command.EmployeeId = Guid.NewGuid();

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EmployeeId);
        _mockUserService.Verify(x => x.ExistsWithEmployeeIdAsync(It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }
}
