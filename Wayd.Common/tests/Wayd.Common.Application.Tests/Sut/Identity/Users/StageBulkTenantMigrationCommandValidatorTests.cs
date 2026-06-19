using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.Users;

namespace Wayd.Common.Application.Tests.Sut.Identity.Users;

public class StageBulkTenantMigrationCommandValidatorTests
{
    private readonly StageBulkTenantMigrationCommandValidator _sut = new();

    private static StageBulkTenantMigrationCommand CreateValidCommand() => new(
        ProviderId: Guid.NewGuid(),
        SourceTenantId: Guid.NewGuid().ToString(),
        TargetTenantId: Guid.NewGuid().ToString(),
        UserIds: ["user-1", "user-2"]);

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange
        var command = CreateValidCommand();

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenProviderIdIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand() with { ProviderId = Guid.Empty };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.ProviderId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSourceTenantIsNotAGuid()
    {
        // Arrange
        var command = CreateValidCommand() with { SourceTenantId = "not-a-guid" };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.SourceTenantId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTargetTenantIsNotAGuid()
    {
        // Arrange
        var command = CreateValidCommand() with { TargetTenantId = "not-a-guid" };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetTenantId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTargetTenantEqualsSourceTenant()
    {
        // Arrange
        var sameTenant = Guid.NewGuid().ToString();
        var command = CreateValidCommand() with { SourceTenantId = sameTenant, TargetTenantId = sameTenant };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.TargetTenantId)
            .WithErrorMessage("Target tenant must differ from the source tenant.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdsIsEmpty()
    {
        // Arrange
        var command = CreateValidCommand() with { UserIds = [] };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserIds);
    }

    [Fact]
    public void Validate_ShouldFailCleanly_WhenUserIdsIsNull()
    {
        // Arrange — a null list must produce a clean validation failure, not throw on
        // the subsequent count check (which would surface as a 500 instead of a 422).
        var command = CreateValidCommand() with { UserIds = null! };

        // Act — must not throw; the per-property Stop cascade short-circuits after NotEmpty.
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserIds);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdsExceedsCap()
    {
        // Arrange — one over the 500 limit.
        var tooMany = Enumerable.Range(0, StageBulkTenantMigrationCommandValidator.MaxUserIds + 1)
            .Select(i => $"user-{i}")
            .ToList();
        var command = CreateValidCommand() with { UserIds = tooMany };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.UserIds);
    }

    [Fact]
    public void Validate_ShouldPass_WhenUserIdsIsExactlyAtCap()
    {
        // Arrange — exactly 500 is allowed.
        var atCap = Enumerable.Range(0, StageBulkTenantMigrationCommandValidator.MaxUserIds)
            .Select(i => $"user-{i}")
            .ToList();
        var command = CreateValidCommand() with { UserIds = atCap };

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(c => c.UserIds);
    }
}
