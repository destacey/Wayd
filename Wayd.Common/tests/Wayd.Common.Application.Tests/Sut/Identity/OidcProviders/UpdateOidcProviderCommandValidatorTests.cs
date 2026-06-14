using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.OidcProviders.Commands;
using Wayd.Common.Application.Identity.Roles;

namespace Wayd.Common.Application.Tests.Sut.Identity.OidcProviders;

/// <summary>
/// Update is the smaller-surface sibling of Create — no Name uniqueness check
/// (Name is immutable), no Entra-tenant-list rule (handler enforces it because
/// it needs the persisted ProviderType). Tests cover the field-shape rules
/// that DO apply.
/// </summary>
public class UpdateOidcProviderCommandValidatorTests
{
    private const string ExistingRoleId = "role-existing";

    private readonly Mock<IRoleService> _roleService;
    private readonly UpdateOidcProviderCommandValidator _sut;

    public UpdateOidcProviderCommandValidatorTests()
    {
        _roleService = new Mock<IRoleService>();
        _roleService.Setup(s => s.GetById(ExistingRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleDto { Id = ExistingRoleId, Name = "Existing" });
        _sut = new UpdateOidcProviderCommandValidator(_roleService.Object);
    }

    private static UpdateOidcProviderCommand ValidCommand() => new(
        Id: Guid.NewGuid(),
        DisplayName: "Acme Entra",
        Authority: "https://login.microsoftonline.com/common/v2.0",
        ClientId: "test-client-id",
        Audience: "api://test",
        Scopes: new[] { "openid", "profile" },
        AllowedTenantIds: new[] { "11111111-1111-1111-1111-111111111111" },
        ClockSkewSeconds: 60,
        IsEnabled: true,
        AllowAutoRegistration: true,
        RequireEmployeeRecord: true,
        DefaultRoleId: ExistingRoleId);

    [Fact]
    public async Task Validate_WithValidCommand_PassesAllRules()
    {
        var result = await _sut.TestValidateAsync(ValidCommand(), cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithEmptyId_FailsIdRule()
    {
        var command = ValidCommand() with { Id = Guid.Empty };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Theory]
    [InlineData("http://login.microsoftonline.com/common/v2.0")]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public async Task Validate_WithNonHttpsAuthority_FailsAuthorityRule(string authority)
    {
        var command = ValidCommand() with { Authority = authority };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Authority);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyDisplayName_FailsDisplayNameRule(string displayName)
    {
        var command = ValidCommand() with { DisplayName = displayName };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public async Task Validate_WithDisplayNameTooLong_FailsDisplayNameRule()
    {
        var command = ValidCommand() with { DisplayName = new string('a', 101) };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public async Task Validate_WithClockSkewOutOfRange_FailsClockSkewRule(int clockSkew)
    {
        var command = ValidCommand() with { ClockSkewSeconds = clockSkew };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.ClockSkewSeconds);
    }

    [Fact]
    public async Task Validate_WithExistingDefaultRoleId_PassesRoleRule()
    {
        var command = ValidCommand() with { DefaultRoleId = ExistingRoleId };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultRoleId);
    }

    [Fact]
    public async Task Validate_WithUnknownDefaultRoleId_FailsRoleRule()
    {
        var command = ValidCommand() with { DefaultRoleId = "does-not-exist" };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.DefaultRoleId)
            .WithErrorMessage("The selected default role does not exist.");
    }
}
