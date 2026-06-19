using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Sut.Identity;

/// <summary>
/// Pins the security-relevant invariants on <see cref="OidcProvider"/>: HTTPS
/// authority, Entra-requires-tenants, reserved provider names, immutability of
/// <c>Name</c> and <c>ProviderType</c> through the Update path, and tenant-id
/// normalization. These rules are the last line of defense against
/// misconfiguration before the row hits the database, where the registry/
/// validator will trust whatever it sees.
/// </summary>
public sealed class OidcProviderTests
{
    private static readonly Instant Timestamp = SystemClock.Instance.GetCurrentInstant();
    private const string ValidName = "MicrosoftEntraId";
    private const string ValidDisplayName = "Microsoft Entra ID";
    private const string ValidEntraAuthority = "https://login.microsoftonline.com/common/v2.0";
    private const string ValidGenericAuthority = "https://accounts.google.com";
    private const string ValidClientId = "test-client-id";
    private const string ValidAudience = "api://test";
    private const string ValidTenantId = "11111111-1111-1111-1111-111111111111";

    private static readonly string[] ValidScopes = { "openid", "profile" };
    private static readonly string[] ValidAllowedTenants = { ValidTenantId };

    // --- Create: happy paths ---

    [Fact]
    public void Create_WithValidEntraConfig_ReturnsSuccess()
    {
        var result = OidcProvider.Create(
            name: ValidName,
            displayName: ValidDisplayName,
            providerType: OidcProviderType.MicrosoftEntraId,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsSuccess.Should().BeTrue();
        var provider = result.Value;
        provider.Name.Should().Be(ValidName);
        provider.DisplayName.Should().Be(ValidDisplayName);
        provider.ProviderType.Should().Be(OidcProviderType.MicrosoftEntraId);
        provider.Authority.Should().Be(ValidEntraAuthority);
        provider.ClientId.Should().Be(ValidClientId);
        provider.Audience.Should().Be(ValidAudience);
        provider.Scopes.Should().BeEquivalentTo(ValidScopes);
        provider.AllowedTenantIds.Should().BeEquivalentTo(ValidAllowedTenants);
        provider.ClockSkewSeconds.Should().Be(60);
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_WithGenericOidcAndNoTenantList_ReturnsSuccess()
    {
        // GenericOidc providers don't require a tenant list — the entity must
        // accept null/empty here even though Entra would reject it.
        var result = OidcProvider.Create(
            name: "Acme-Google",
            displayName: "Acme Google",
            providerType: OidcProviderType.GenericOidc,
            authority: ValidGenericAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: null,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.AllowedTenantIds.Should().BeNull();
    }

    // --- Create: Authority validation ---

    [Theory]
    [InlineData("http://login.microsoftonline.com/common/v2.0")]  // HTTP rejected
    [InlineData("ftp://example.com")]                              // wrong scheme
    [InlineData("/relative/path")]                                 // not absolute
    [InlineData("not-a-url")]                                      // not a URI
    [InlineData("")]                                               // empty
    [InlineData("   ")]                                            // whitespace
    public void Create_WithNonHttpsAuthority_ReturnsFailure(string authority)
    {
        var result = CreateEntra(authority: authority);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("HTTPS");
    }

    // --- Create: Entra-specific tenant rule ---

    [Fact]
    public void Create_EntraWithNullAllowedTenantIds_ReturnsFailure()
    {
        // The security gate: Entra issues tokens at /common/ for every tenant.
        // Without a tenant allowlist the validator has no way to reject
        // tokens from tenants we haven't onboarded. Bypass the helper because
        // it falls back to a valid default when null is passed — we need null
        // to reach the entity.
        var result = OidcProvider.Create(
            name: ValidName,
            displayName: ValidDisplayName,
            providerType: OidcProviderType.MicrosoftEntraId,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: null,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AllowedTenantId");
    }

    [Fact]
    public void Create_EntraWithEmptyAllowedTenantIds_ReturnsFailure()
    {
        var result = CreateEntra(allowedTenantIds: Array.Empty<string>());

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_EntraWithOnlyWhitespaceTenants_ReturnsFailure()
    {
        // Trim-then-check: a tenant list of all-whitespace strings counts as
        // empty. NormalizeAllowedTenantIds drops blanks, so leaving these in
        // would otherwise produce a "tenant-less" provider.
        var result = CreateEntra(allowedTenantIds: new[] { "", "   " });

        result.IsFailure.Should().BeTrue();
    }

    // --- Create: reserved name ---

    [Theory]
    [InlineData("Wayd")]
    [InlineData("wayd")]
    [InlineData("WAYD")]
    [InlineData("  Wayd  ")]
    public void Create_WithReservedWaydName_ReturnsFailure(string name)
    {
        // "Wayd" is the UserIdentity provider for local accounts. An OIDC
        // provider claiming this name could impersonate local users at
        // identity lookup time.
        var result = CreateEntra(name: name);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("reserved");
    }

    // --- Create: ClockSkew bounds ---

    [Theory]
    [InlineData(-1)]
    [InlineData(601)]
    public void Create_WithClockSkewOutOfRange_ReturnsFailure(int clockSkew)
    {
        var result = CreateEntra(clockSkewSeconds: clockSkew);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ClockSkewSeconds");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(600)]
    public void Create_WithClockSkewInRange_ReturnsSuccess(int clockSkew)
    {
        var result = CreateEntra(clockSkewSeconds: clockSkew);

        result.IsSuccess.Should().BeTrue();
        result.Value.ClockSkewSeconds.Should().Be(clockSkew);
    }

    // --- Create: AllowedTenantIds normalization ---

    [Fact]
    public void Create_NormalizesAllowedTenantIds_TrimsAndDedupes()
    {
        // Operators often paste tenant IDs with stray whitespace or
        // accidentally duplicate them. The entity normalizes to keep the
        // runtime allowlist clean.
        var result = CreateEntra(allowedTenantIds: new[]
        {
            ValidTenantId,
            $"  {ValidTenantId.ToUpperInvariant()}  ",  // duplicate, different case + whitespace
            "",  // dropped
            "22222222-2222-2222-2222-222222222222",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.AllowedTenantIds.Should().HaveCount(2);
        result.Value.AllowedTenantIds.Should().Contain(ValidTenantId);
        result.Value.AllowedTenantIds.Should().Contain("22222222-2222-2222-2222-222222222222");
    }

    // --- Domain events ---

    [Fact]
    public void Create_AddsEntityCreatedEvent()
    {
        var result = CreateEntra();

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().HaveCount(1);
        result.Value.DomainEvents.Single().Should().BeOfType<EntityCreatedEvent<OidcProvider>>();
    }

    // --- Update: happy path ---

    [Fact]
    public void Update_WithValidFields_ChangesMutableFieldsOnly()
    {
        var provider = EntraFixture().Generate();
        provider.ClearDomainEvents();

        var result = provider.Update(
            displayName: "Renamed",
            authority: "https://login.microsoftonline.com/organizations/v2.0",
            clientId: "new-client",
            audience: "api://new",
            scopes: new[] { "openid", "profile", "email" },
            allowedTenantIds: new[] { "22222222-2222-2222-2222-222222222222" },
            clockSkewSeconds: 120,
            isEnabled: false,
            timestamp: Timestamp);

        result.IsSuccess.Should().BeTrue();
        provider.DisplayName.Should().Be("Renamed");
        provider.Authority.Should().Be("https://login.microsoftonline.com/organizations/v2.0");
        provider.ClientId.Should().Be("new-client");
        provider.Audience.Should().Be("api://new");
        provider.Scopes.Should().BeEquivalentTo(new[] { "openid", "profile", "email" });
        provider.AllowedTenantIds.Should().BeEquivalentTo(new[] { "22222222-2222-2222-2222-222222222222" });
        provider.ClockSkewSeconds.Should().Be(120);
        provider.IsEnabled.Should().BeFalse();

        // Name and ProviderType are immutable post-Create. Asserting them here
        // pins that Update doesn't accept them.
        provider.Name.Should().Be(ValidName);
        provider.ProviderType.Should().Be(OidcProviderType.MicrosoftEntraId);

        provider.DomainEvents.Should().ContainSingle(e => e is EntityUpdatedEvent<OidcProvider>);
    }

    // --- Update: invariants still enforced ---

    [Fact]
    public void Update_WithNonHttpsAuthority_ReturnsFailure()
    {
        // Same Authority invariant as Create. Update could otherwise downgrade
        // a previously-secure provider config.
        var provider = EntraFixture().Generate();

        var result = provider.Update(
            displayName: ValidDisplayName,
            authority: "http://insecure.example.com",
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("HTTPS");
    }

    [Fact]
    public void Update_EntraToEmptyAllowedTenantIds_ReturnsFailure()
    {
        // The Entra tenant-allowlist invariant must hold across Updates too.
        // Without this an admin could disable the multi-tenant gate post-create.
        var provider = EntraFixture().Generate();

        var result = provider.Update(
            displayName: ValidDisplayName,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: Array.Empty<string>(),
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Update_GenericOidcToEmptyAllowedTenantIds_ReturnsSuccess()
    {
        // GenericOidc doesn't care about the tenant list — Update should let
        // an admin drop it without complaint.
        var provider = OidcProvider.Create(
            name: "Acme-Google",
            displayName: "Acme Google",
            providerType: OidcProviderType.GenericOidc,
            authority: ValidGenericAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: new[] { "irrelevant" },  // tolerated on Create, ignored at runtime
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp).Value;

        var result = provider.Update(
            displayName: "Acme Google",
            authority: ValidGenericAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: null,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp);

        result.IsSuccess.Should().BeTrue();
        provider.AllowedTenantIds.Should().BeNull();
    }

    // --- SetEnabled ---

    [Fact]
    public void SetEnabled_FromEnabledToDisabled_AddsUpdateEvent()
    {
        var provider = EntraFixture().AsEnabled().Generate();
        provider.ClearDomainEvents();

        var result = provider.SetEnabled(false, Timestamp);

        result.IsSuccess.Should().BeTrue();
        provider.IsEnabled.Should().BeFalse();
        provider.DomainEvents.Should().ContainSingle(e => e is EntityUpdatedEvent<OidcProvider>);
    }

    [Fact]
    public void SetEnabled_WithNoChange_NoOpsAndAddsNoEvent()
    {
        // Idempotent: re-applying the current state shouldn't generate an
        // audit event. Saves event-handler work for admins clicking the toggle
        // twice.
        var provider = EntraFixture().AsEnabled().Generate();
        provider.ClearDomainEvents();

        var result = provider.SetEnabled(true, Timestamp);

        result.IsSuccess.Should().BeTrue();
        provider.IsEnabled.Should().BeTrue();
        provider.DomainEvents.Should().BeEmpty();
    }

    // --- Registration policy ---

    [Fact]
    public void Create_WithoutPolicyArgs_DefaultsToAutoRegistrationOff()
    {
        // Secure by default: omitting the policy args creates a provider with
        // auto-registration disabled. The dependent settings have no value in that
        // state — the nullable projections read null.
        var provider = EntraFixture().Generate();

        provider.RegistrationPolicy.AllowAutoRegistration.Should().BeFalse();
        provider.RegistrationPolicy.RequireEmployeeRecord.Should().BeNull();
        provider.RegistrationPolicy.DefaultRoleId.Should().BeNull();
    }

    [Fact]
    public void Create_WithAutoRegistrationOn_PersistsDependentFields()
    {
        var result = OidcProvider.Create(
            name: ValidName,
            displayName: ValidDisplayName,
            providerType: OidcProviderType.MicrosoftEntraId,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp,
            registrationPolicy: RegistrationPolicy.Enabled(requireEmployeeRecord: false, defaultRoleId: "role-123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.RegistrationPolicy.AllowAutoRegistration.Should().BeTrue();
        result.Value.RegistrationPolicy.RequireEmployeeRecord.Should().BeFalse();
        result.Value.RegistrationPolicy.DefaultRoleId.Should().Be("role-123");
    }

    [Fact]
    public void Create_WithAutoRegistrationOff_IgnoresDependentFields()
    {
        // The invariant: when auto-registration is off, the dependent settings
        // can't carry a value — FromFlat discards loosened inputs, so the stored
        // policy projections read null regardless of what the caller passed.
        var result = OidcProvider.Create(
            name: ValidName,
            displayName: ValidDisplayName,
            providerType: OidcProviderType.MicrosoftEntraId,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp,
            registrationPolicy: RegistrationPolicy.FromFlat(
                allowAutoRegistration: false, requireEmployeeRecord: false, defaultRoleId: "role-123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.RegistrationPolicy.AllowAutoRegistration.Should().BeFalse();
        result.Value.RegistrationPolicy.RequireEmployeeRecord.Should().BeNull();
        result.Value.RegistrationPolicy.DefaultRoleId.Should().BeNull();
    }

    [Fact]
    public void Update_WithAutoRegistrationOn_ChangesDependentFields()
    {
        var provider = EntraFixture().Generate();

        var result = provider.Update(
            displayName: ValidDisplayName,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp,
            registrationPolicy: RegistrationPolicy.Enabled(requireEmployeeRecord: false, defaultRoleId: " role-xyz "));

        result.IsSuccess.Should().BeTrue();
        provider.RegistrationPolicy.AllowAutoRegistration.Should().BeTrue();
        provider.RegistrationPolicy.RequireEmployeeRecord.Should().BeFalse();
        provider.RegistrationPolicy.DefaultRoleId.Should().Be("role-xyz");
    }

    [Fact]
    public void Update_TurningAutoRegistrationOff_ClearsDependentFields()
    {
        // Start from an auto-registering provider with a custom role, then disable
        // auto-registration. The dependent fields must not survive — a later
        // re-enable shouldn't silently inherit the old loosened posture.
        var provider = EntraFixture()
            .WithAutoRegistration(requireEmployeeRecord: false, defaultRoleId: "role-abc")
            .Generate();

        var result = provider.Update(
            displayName: ValidDisplayName,
            authority: ValidEntraAuthority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: ValidAllowedTenants,
            clockSkewSeconds: 60,
            isEnabled: true,
            timestamp: Timestamp,
            registrationPolicy: RegistrationPolicy.Disabled());

        result.IsSuccess.Should().BeTrue();
        provider.RegistrationPolicy.AllowAutoRegistration.Should().BeFalse();
        provider.RegistrationPolicy.RequireEmployeeRecord.Should().BeNull();
        provider.RegistrationPolicy.DefaultRoleId.Should().BeNull();
    }

    // --- Helpers ---

    // CreateEntra exercises the Create factory directly — it is the system under
    // test for the validation cases above, so it must NOT go through the faker
    // (which bypasses Create via field binding and would test nothing).
    private static Result<OidcProvider> CreateEntra(
        string name = ValidName,
        string authority = ValidEntraAuthority,
        IReadOnlyList<string>? allowedTenantIds = null,
        int clockSkewSeconds = 60,
        bool isEnabled = true)
    {
        return OidcProvider.Create(
            name: name,
            displayName: ValidDisplayName,
            providerType: OidcProviderType.MicrosoftEntraId,
            authority: authority,
            clientId: ValidClientId,
            audience: ValidAudience,
            scopes: ValidScopes,
            allowedTenantIds: allowedTenantIds ?? ValidAllowedTenants,
            clockSkewSeconds: clockSkewSeconds,
            isEnabled: isEnabled,
            timestamp: Timestamp);
    }

    // A valid Entra provider built via the faker, for tests that need a starting
    // fixture to act on (Update/SetEnabled) rather than testing Create itself.
    // Name/authority/etc. are pinned to the same constants the Create-based tests
    // use so assertions that check those values stay stable.
    private static OidcProviderFaker EntraFixture() =>
        new OidcProviderFaker()
            .WithName(ValidName)
            .WithDisplayName(ValidDisplayName)
            .AsMicrosoftEntraId(ValidTenantId)
            .WithAuthority(ValidEntraAuthority)
            .WithClientId(ValidClientId)
            .WithAudience(ValidAudience)
            .WithScopes(ValidScopes)
            .WithClockSkewSeconds(60);
}
