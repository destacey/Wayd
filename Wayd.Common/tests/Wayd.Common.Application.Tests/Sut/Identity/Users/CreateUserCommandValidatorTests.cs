using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Identity.Users;

namespace Wayd.Common.Application.Tests.Sut.Identity.Users;

public class CreateUserCommandValidatorTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IRoleService> _mockRoleService;
    private readonly CreateUserCommandValidator _sut;

    public CreateUserCommandValidatorTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockUserService.Setup(x => x.ExistsWithEmailAsync(It.IsAny<string>(), null)).ReturnsAsync(false);
        _mockUserService.Setup(x => x.ExistsWithNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockUserService.Setup(x => x.ExistsWithPhoneNumberAsync(It.IsAny<string>(), null)).ReturnsAsync(false);

        _mockRoleService = new Mock<IRoleService>();
        _mockRoleService.Setup(x => x.Exists(It.IsAny<string>(), null)).ReturnsAsync(true);

        _sut = new CreateUserCommandValidator(_mockUserService.Object, _mockRoleService.Object);
    }

    private static CreateUserCommand CreateValidWaydCommand() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john.doe@example.com",
        LoginProvider = LoginProviders.Wayd,
        Password = "Password123!",
        RoleNames = ["Contributor"],
    };

    private static CreateUserCommand CreateValidEntraIdCommand() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        LoginProvider = LoginProviders.MicrosoftEntraId,
        Password = null,
        RoleNames = ["Contributor"],
    };

    #region Valid Commands

    [Fact]
    public async Task Validate_ShouldPass_WhenWaydCommandIsValid()
    {
        // Arrange
        var command = CreateValidWaydCommand();

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenEntraIdCommandIsValid()
    {
        // Arrange
        var command = CreateValidEntraIdCommand();

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region LoginProvider Validation

    [Fact]
    public async Task Validate_ShouldFail_WhenLoginProviderIsEmpty()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.LoginProvider = string.Empty;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LoginProvider);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenLoginProviderIsInvalid()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.LoginProvider = "InvalidProvider";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LoginProvider)
            .WithErrorMessage("Login provider must be one of: " + string.Join(", ", LoginProviders.All));
    }

    #endregion

    #region Password Validation

    [Fact]
    public async Task Validate_ShouldFail_WhenWaydAccountHasNoPassword()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Password = null;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required for Wayd accounts.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenWaydAccountPasswordIsTooShort()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Password = "short";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPasswordHasNoUppercase()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Password = "password1";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPasswordHasNoLowercase()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Password = "PASSWORD1";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPasswordHasNoDigit()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Password = "Passwords";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNonWaydAccountHasPassword()
    {
        // Arrange
        var command = CreateValidEntraIdCommand();
        command.Password = "ShouldNotBeHere123!";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not be provided for non-Wayd accounts.");
    }

    #endregion

    #region Email Validation

    [Fact]
    public async Task Validate_ShouldFail_WhenEmailIsEmpty()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Email = string.Empty;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEmailIsInvalid()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.Email = "not-an-email";

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid Email Address.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEmailAlreadyExists()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        _mockUserService.Setup(x => x.ExistsWithEmailAsync(command.Email, null)).ReturnsAsync(true);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage($"Email {command.Email} is already registered.");
    }

    #endregion

    #region Name Validation

    [Fact]
    public async Task Validate_ShouldFail_WhenFirstNameIsEmpty()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.FirstName = string.Empty;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData(101)]
    public async Task Validate_ShouldFail_WhenFirstNameExceedsMaxLength(int length)
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.FirstName = new string('A', length);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenLastNameIsEmpty()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.LastName = string.Empty;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    #endregion

    #region PhoneNumber Validation

    [Fact]
    public async Task Validate_ShouldPass_WhenPhoneNumberIsNull()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.PhoneNumber = null;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenPhoneNumberAlreadyExists()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.PhoneNumber = "555-1234";
        _mockUserService.Setup(x => x.ExistsWithPhoneNumberAsync("555-1234", null)).ReturnsAsync(true);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    #endregion

    #region Roles Validation

    [Fact]
    public async Task Validate_ShouldFail_WhenNoRolesAssigned()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.RoleNames = [];

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleNames)
            .WithErrorMessage("At least one role must be assigned.");
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenRoleDoesNotExist()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.RoleNames = ["Ghost"];
        _mockRoleService.Setup(x => x.Exists("Ghost", null)).ReturnsAsync(false);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor("RoleNames[0]")
            .WithErrorMessage("Role Ghost does not exist.");
    }

    [Fact]
    public async Task Validate_ShouldFailGracefully_WhenRoleNamesIsNull()
    {
        // Arrange — a `roleNames: null` payload must surface as a validation failure,
        // not an unhandled NullReferenceException (500).
        var command = CreateValidWaydCommand();
        command.RoleNames = null!;

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RoleNames)
            .WithErrorMessage("At least one role must be assigned.");
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenMultipleExistingRolesAssigned()
    {
        // Arrange
        var command = CreateValidWaydCommand();
        command.RoleNames = ["Contributor", "ProjectManager"];

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RoleNames);
    }

    #endregion
}
