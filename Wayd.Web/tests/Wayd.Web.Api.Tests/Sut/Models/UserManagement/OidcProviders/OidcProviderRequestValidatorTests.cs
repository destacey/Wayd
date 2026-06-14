using FluentValidation.TestHelper;
using Wayd.Common.Domain.Identity;
using Wayd.Web.Api.Models.UserManagement.OidcProviders;

namespace Wayd.Web.Api.Tests.Sut.Models.UserManagement.OidcProviders;

public sealed class OidcProviderRequestValidatorTests
{
    private readonly CreateOidcProviderRequestValidator _createValidator = new();
    private readonly UpdateOidcProviderRequestValidator _updateValidator = new();

    private static CreateOidcProviderRequest ValidCreateRequest() => new()
    {
        Name = "acme-oidc",
        DisplayName = "Acme OIDC",
        ProviderType = OidcProviderType.GenericOidc,
        Authority = "https://login.example.com/v2.0",
        ClientId = "client-123",
        Audience = "api://client-123",
        Scopes = ["openid", "profile"],
        AllowedTenantIds = null,
        ClockSkewSeconds = 60,
        IsEnabled = true,
        AllowAutoRegistration = false,
        RequireEmployeeRecord = null,
        DefaultRoleId = null,
    };

    private static UpdateOidcProviderRequest ValidUpdateRequest() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Acme OIDC",
        Authority = "https://login.example.com/v2.0",
        ClientId = "client-123",
        Audience = "api://client-123",
        Scopes = ["openid", "profile"],
        AllowedTenantIds = null,
        ClockSkewSeconds = 60,
        IsEnabled = true,
        AllowAutoRegistration = false,
        RequireEmployeeRecord = null,
        DefaultRoleId = null,
    };

    [Fact]
    public void Create_DisabledRegistration_WithNoDependentFields_Passes()
    {
        // Arrange
        var request = ValidCreateRequest();

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RequireEmployeeRecord);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultRoleId);
    }

    [Fact]
    public void Create_EnabledRegistration_WithGateAndRole_Passes()
    {
        // Arrange
        var request = ValidCreateRequest() with
        {
            AllowAutoRegistration = true,
            RequireEmployeeRecord = true,
            DefaultRoleId = "role-1",
        };

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.RequireEmployeeRecord);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultRoleId);
    }

    [Fact]
    public void Create_EnabledRegistration_MissingGate_FailsRequireEmployeeRecord()
    {
        // Arrange
        var request = ValidCreateRequest() with
        {
            AllowAutoRegistration = true,
            RequireEmployeeRecord = null,
            DefaultRoleId = "role-1",
        };

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(CreateOidcProviderRequest.RequireEmployeeRecord))
            .WithErrorMessage(OidcProviderRequestMessages.EmployeeGateRequired);
    }

    [Fact]
    public void Create_EnabledRegistration_MissingRole_FailsDefaultRoleId()
    {
        // Arrange
        var request = ValidCreateRequest() with
        {
            AllowAutoRegistration = true,
            RequireEmployeeRecord = true,
            DefaultRoleId = null,
        };

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(CreateOidcProviderRequest.DefaultRoleId))
            .WithErrorMessage(OidcProviderRequestMessages.DefaultRoleRequired);
    }

    [Fact]
    public void Create_DisabledRegistration_WithGate_FailsRequireEmployeeRecord()
    {
        // Arrange
        var request = ValidCreateRequest() with
        {
            AllowAutoRegistration = false,
            RequireEmployeeRecord = true,
        };

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(CreateOidcProviderRequest.RequireEmployeeRecord))
            .WithErrorMessage(OidcProviderRequestMessages.EmployeeGateForbidden);
    }

    [Fact]
    public void Create_DisabledRegistration_WithRole_FailsDefaultRoleId()
    {
        // Arrange
        var request = ValidCreateRequest() with
        {
            AllowAutoRegistration = false,
            DefaultRoleId = "role-1",
        };

        // Act
        var result = _createValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(CreateOidcProviderRequest.DefaultRoleId))
            .WithErrorMessage(OidcProviderRequestMessages.DefaultRoleForbidden);
    }

    [Fact]
    public void Update_EnabledRegistration_MissingGate_FailsRequireEmployeeRecord()
    {
        // Arrange
        var request = ValidUpdateRequest() with
        {
            AllowAutoRegistration = true,
            RequireEmployeeRecord = null,
            DefaultRoleId = "role-1",
        };

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(UpdateOidcProviderRequest.RequireEmployeeRecord))
            .WithErrorMessage(OidcProviderRequestMessages.EmployeeGateRequired);
    }

    [Fact]
    public void Update_DisabledRegistration_WithRole_FailsDefaultRoleId()
    {
        // Arrange
        var request = ValidUpdateRequest() with
        {
            AllowAutoRegistration = false,
            DefaultRoleId = "role-1",
        };

        // Act
        var result = _updateValidator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(nameof(UpdateOidcProviderRequest.DefaultRoleId))
            .WithErrorMessage(OidcProviderRequestMessages.DefaultRoleForbidden);
    }
}
