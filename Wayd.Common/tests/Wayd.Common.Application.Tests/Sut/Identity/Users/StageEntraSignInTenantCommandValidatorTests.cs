using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.Users;

namespace Wayd.Common.Application.Tests.Sut.Identity.Users;

public class StageEntraSignInTenantCommandValidatorTests
{
    private readonly StageEntraSignInTenantCommandValidator _sut = new();

    private const string TenantId = "7d1b4a52-0000-4000-8000-000000000001";

    [Fact]
    public async Task Validate_ShouldPass_WhenTenantIsChosen()
    {
        // Arrange
        var command = new StageEntraSignInTenantCommand("user-1", TenantId);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenNoTenantIsChosen()
    {
        // Arrange — the provider's only allowed tenant is used instead.
        var command = new StageEntraSignInTenantCommand("user-1", null);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUserIdIsEmpty()
    {
        // Arrange
        var command = new StageEntraSignInTenantCommand(string.Empty, TenantId);

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenTenantExceedsMaxLength()
    {
        // Arrange — matches the PendingMigrationTenantId column.
        var command = new StageEntraSignInTenantCommand("user-1", new string('a', 101));

        // Act
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }
}
