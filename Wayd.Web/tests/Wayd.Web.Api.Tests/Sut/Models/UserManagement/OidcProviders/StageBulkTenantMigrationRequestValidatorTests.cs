using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.Users;
using Wayd.Web.Api.Models.UserManagement.OidcProviders;

namespace Wayd.Web.Api.Tests.Sut.Models.UserManagement.OidcProviders;

public sealed class StageBulkTenantMigrationRequestValidatorTests
{
    private readonly StageBulkTenantMigrationRequestValidator _sut = new();

    private static StageBulkTenantMigrationRequest ValidRequest() => new(
        SourceTenantId: Guid.NewGuid().ToString(),
        TargetTenantId: Guid.NewGuid().ToString(),
        UserIds: ["user-1"]);

    [Fact]
    public void Validate_ShouldPass_WhenRequestIsValid()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenSourceTenantIsNotAGuid()
    {
        // Arrange
        var request = ValidRequest() with { SourceTenantId = "nope" };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.SourceTenantId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTargetTenantEqualsSource()
    {
        // Arrange
        var same = Guid.NewGuid().ToString();
        var request = ValidRequest() with { SourceTenantId = same, TargetTenantId = same };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.TargetTenantId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdsIsEmpty()
    {
        // Arrange
        var request = ValidRequest() with { UserIds = [] };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.UserIds);
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdsExceedsCap()
    {
        // Arrange
        var tooMany = Enumerable.Range(0, StageBulkTenantMigrationCommandValidator.MaxUserIds + 1)
            .Select(i => $"user-{i}")
            .ToList();
        var request = ValidRequest() with { UserIds = tooMany };

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.UserIds);
    }
}
