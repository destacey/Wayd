using FluentValidation.TestHelper;
using Wayd.Common.Application.Identity.OidcProviders.Commands;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Application.Tests.Sut.Identity.OidcProviders;

/// <summary>
/// FluentValidation rule tests for <see cref="CreateOidcProviderCommandValidator"/>.
/// The entity-level invariants (in <see cref="OidcProviderTests"/>) are the
/// second line of defense; the validator is the first, surfacing clear 400s
/// before the request ever reaches the handler.
/// </summary>
public class CreateOidcProviderCommandValidatorTests
{
    private const string ExistingRoleId = "role-existing";

    private readonly FakeWaydDbContext _dbContext;
    private readonly Mock<IRoleService> _roleService;
    private readonly CreateOidcProviderCommandValidator _sut;

    public CreateOidcProviderCommandValidatorTests()
    {
        _dbContext = new FakeWaydDbContext();
        _roleService = new Mock<IRoleService>();
        // Default: the configured default role exists. Tests that need a missing
        // role override this per-case.
        _roleService.Setup(s => s.GetById(ExistingRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleDto { Id = ExistingRoleId, Name = "Existing" });
        _sut = new CreateOidcProviderCommandValidator(_dbContext, _roleService.Object);
    }

    private static CreateOidcProviderCommand ValidEntraCommand() => new(
        Name: "Acme-Entra",
        DisplayName: "Acme Entra",
        ProviderType: OidcProviderType.MicrosoftEntraId,
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

    private static CreateOidcProviderCommand ValidGenericCommand() => new(
        Name: "Acme-Google",
        DisplayName: "Acme Google",
        ProviderType: OidcProviderType.GenericOidc,
        Authority: "https://accounts.google.com",
        ClientId: "test-client-id",
        Audience: "api://test",
        Scopes: new[] { "openid", "profile" },
        AllowedTenantIds: null,
        ClockSkewSeconds: 60,
        IsEnabled: true,
        AllowAutoRegistration: true,
        RequireEmployeeRecord: true,
        DefaultRoleId: ExistingRoleId);

    [Fact]
    public async Task Validate_WithValidEntraCommand_PassesAllRules()
    {
        var result = await _sut.TestValidateAsync(ValidEntraCommand(), cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_WithValidGenericOidcCommand_PassesAllRules()
    {
        var result = await _sut.TestValidateAsync(ValidGenericCommand(), cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // --- Name uniqueness ---
    // Note: the unique-name rule is intentionally not unit-tested here. The
    // shared FakeWaydDbContext mocks DbSet but doesn't model `.Add()` semantics,
    // so seeding a "duplicate" row to make the rule fail isn't straightforward.
    // The rule itself is a 1-line `AnyAsync`; integration coverage is provided
    // by the unique-index on the underlying database column, which would reject
    // a duplicate even if the validator silently passed.

    // --- Authority shape ---

    [Theory]
    [InlineData("http://login.microsoftonline.com/common/v2.0")]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("/relative")]
    public async Task Validate_WithNonHttpsAuthority_FailsAuthorityRule(string authority)
    {
        var command = ValidEntraCommand() with { Authority = authority };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Authority);
    }

    // --- Entra tenant-list requirement ---

    [Fact]
    public async Task Validate_EntraWithNullTenantList_FailsAllowedTenantIdsRule()
    {
        var command = ValidEntraCommand() with { AllowedTenantIds = null };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.AllowedTenantIds);
    }

    [Fact]
    public async Task Validate_EntraWithEmptyTenantList_FailsAllowedTenantIdsRule()
    {
        var command = ValidEntraCommand() with { AllowedTenantIds = Array.Empty<string>() };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.AllowedTenantIds);
    }

    [Fact]
    public async Task Validate_EntraWithWhitespaceOnlyTenants_FailsAllowedTenantIdsRule()
    {
        var command = ValidEntraCommand() with { AllowedTenantIds = new[] { "", "   " } };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.AllowedTenantIds);
    }

    [Fact]
    public async Task Validate_GenericOidcWithoutTenantList_PassesAllowedTenantIdsRule()
    {
        // The Entra-only rule must NOT fire for GenericOidc — operators can
        // configure a Google/Auth0 provider without supplying a tenant list.
        var command = ValidGenericCommand() with { AllowedTenantIds = null };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveValidationErrorFor(x => x.AllowedTenantIds);
    }

    // --- Reserved name ---

    [Theory]
    [InlineData("Wayd")]
    [InlineData("wayd")]
    [InlineData("WAYD")]
    [InlineData("  Wayd  ")]
    public async Task Validate_WithReservedWaydName_FailsNameRule(string name)
    {
        var command = ValidEntraCommand() with { Name = name };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("'Wayd' is a reserved provider name.");
    }

    // --- Default role ---

    [Fact]
    public async Task Validate_WithNullDefaultRoleId_WhenAutoRegistrationOn_FailsRoleRule()
    {
        // A default role is required when auto-registration is enabled.
        var command = ValidEntraCommand() with { DefaultRoleId = null };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.DefaultRoleId)
            .WithErrorMessage("A default role is required when auto-registration is enabled.");
    }

    [Fact]
    public async Task Validate_WithNullDefaultRoleId_WhenAutoRegistrationOff_PassesRoleRule()
    {
        // When auto-registration is off the role is irrelevant and not validated.
        var command = ValidEntraCommand() with { AllowAutoRegistration = false, DefaultRoleId = null };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultRoleId);
    }

    [Fact]
    public async Task Validate_WithExistingDefaultRoleId_PassesRoleRule()
    {
        var command = ValidEntraCommand() with { DefaultRoleId = ExistingRoleId };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldNotHaveValidationErrorFor(x => x.DefaultRoleId);
    }

    [Fact]
    public async Task Validate_WithUnknownDefaultRoleId_FailsRoleRule()
    {
        // GetById returns null for an unknown id (the default Mock behavior).
        var command = ValidEntraCommand() with { DefaultRoleId = "does-not-exist" };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.DefaultRoleId)
            .WithErrorMessage("The selected default role does not exist.");
    }

    // --- Field-level shape ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptyName_FailsNameRule(string name)
    {
        var command = ValidEntraCommand() with { Name = name };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_WithNameTooLong_FailsNameRule()
    {
        var command = ValidEntraCommand() with { Name = new string('a', 51) };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public async Task Validate_WithClockSkewOutOfRange_FailsClockSkewRule(int clockSkew)
    {
        var command = ValidEntraCommand() with { ClockSkewSeconds = clockSkew };
        var result = await _sut.TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);
        result.ShouldHaveValidationErrorFor(x => x.ClockSkewSeconds);
    }
}
