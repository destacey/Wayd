using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Auth.Oidc;
using Wayd.Tests.Shared.Data;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Oidc;

/// <summary>
/// End-to-end token-validation tests. The validator is the security boundary for
/// token acceptance, so these cover every failure path explicitly — signature,
/// audience, lifetime, algorithm, issuer, tenant allowlist, provider state,
/// blank inputs. Happy-path tests pin the contract for both ProviderType
/// branches (Entra + GenericOidc).
/// </summary>
public class OidcTokenValidatorTests
{
    private const string TestAudience = "api://test-wayd-api";
    private const string AllowedTenantId = "11111111-1111-1111-1111-111111111111";
    private const string OtherTenantId = "22222222-2222-2222-2222-222222222222";
    private const string EntraProviderName = "MicrosoftEntraId";
    private const string GenericProviderName = "Acme-Google";
    private const string EntraAuthority = "https://login.microsoftonline.com/common/v2.0";
    private const string GenericAuthority = "https://accounts.google.com";

    private readonly RsaSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly Mock<IOidcProviderRegistry> _mockRegistry;
    private readonly Mock<IOidcConfigurationManagerFactory> _mockConfigManagerFactory;
    private readonly Mock<IConfigurationManager<OpenIdConnectConfiguration>> _mockOidcConfigManager;
    private readonly ILogger<OidcTokenValidator> _logger;

    public OidcTokenValidatorTests()
    {
        var rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key-1" };
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);

        var oidcConfig = new OpenIdConnectConfiguration { Issuer = GenericAuthority };
        oidcConfig.SigningKeys.Add(_signingKey);

        _mockOidcConfigManager = new Mock<IConfigurationManager<OpenIdConnectConfiguration>>();
        _mockOidcConfigManager
            .Setup(m => m.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(oidcConfig);

        _mockConfigManagerFactory = new Mock<IOidcConfigurationManagerFactory>();
        _mockConfigManagerFactory
            .Setup(f => f.Get(It.IsAny<string>()))
            .Returns(_mockOidcConfigManager.Object);

        _mockRegistry = new Mock<IOidcProviderRegistry>();

        _logger = new LoggerFactory().CreateLogger<OidcTokenValidator>();
    }

    // --- ExtractTenantFromIssuer (pure helper, behaviour ported verbatim from
    //     EntraIdTokenValidator — these pin that it's still correct after the move) ---

    [Fact]
    public void ExtractTenantFromIssuer_WithV1Issuer_ReturnsTenantGuid()
    {
        var result = OidcTokenValidator.ExtractTenantFromIssuer($"https://sts.windows.net/{AllowedTenantId}/");

        result.Should().Be(AllowedTenantId);
    }

    [Fact]
    public void ExtractTenantFromIssuer_WithV2Issuer_ReturnsTenantGuid()
    {
        var result = OidcTokenValidator.ExtractTenantFromIssuer(
            $"https://login.microsoftonline.com/{AllowedTenantId}/v2.0");

        result.Should().Be(AllowedTenantId);
    }

    [Fact]
    public void ExtractTenantFromIssuer_WithCommonPseudoTenant_ReturnsNull()
    {
        var result = OidcTokenValidator.ExtractTenantFromIssuer(
            "https://login.microsoftonline.com/common/v2.0");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public void ExtractTenantFromIssuer_WithUnusableInput_ReturnsNull(string? issuer)
    {
        var result = OidcTokenValidator.ExtractTenantFromIssuer(issuer);

        result.Should().BeNull();
    }

    // --- Input validation ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithBlankProviderName_Throws(string? providerName)
    {
        var sut = CreateSut();
        var act = () => sut.Validate(providerName!, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithBlankToken_Throws(string? token)
    {
        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, token!, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // --- Provider state ---

    [Fact]
    public async Task Validate_WithUnknownProvider_Throws()
    {
        // Registry returns null for unknown names. The validator must reject with
        // the generic "Invalid token" message — not leak that the provider name
        // was unknown vs disabled.
        _mockRegistry
            .Setup(r => r.GetByName(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OidcProvider?)null);

        var sut = CreateSut();
        var act = () => sut.Validate("ghost", BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithDisabledProvider_Throws()
    {
        // Disabled providers stay in the DB but reject exchanges — admin took the
        // provider out of service.
        var provider = CreateEntraProvider(isEnabled: false);
        SetupRegistry(provider);

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithOidcDiscoveryFailure_Throws()
    {
        SetupRegistry(CreateEntraProvider());
        _mockOidcConfigManager
            .Setup(m => m.GetConfigurationAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // --- Entra happy and unhappy paths ---

    [Fact]
    public async Task Validate_Entra_WithValidTokenAndAllowedTenant_ReturnsPrincipal()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var principal = await sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        principal.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.FindFirstValue("tid").Should().Be(AllowedTenantId);
    }

    [Fact]
    public async Task Validate_Entra_WithTenantNotInAllowlist_Throws()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, BuildToken(tid: OtherTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_Entra_AllowlistComparisonIsCaseInsensitive()
    {
        SetupRegistry(CreateEntraProvider(allowedTenantIds: new[] { AllowedTenantId.ToUpperInvariant() }));

        var sut = CreateSut();
        var principal = await sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        principal.Should().NotBeNull();
    }

    [Fact]
    public async Task Validate_Entra_WithNoTidClaim_FallsBackToIssuer()
    {
        // tid is absent (e.g. some guest/personal-account scenarios) — the
        // validator parses the tenant from the signed iss claim.
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var token = BuildToken(
            tid: null,
            issuer: $"https://login.microsoftonline.com/{AllowedTenantId}/v2.0");

        var principal = await sut.Validate(EntraProviderName, token, CancellationToken.None);

        principal.Should().NotBeNull();
    }

    [Fact]
    public async Task Validate_Entra_WithNoTidAndUnparseableIssuer_Throws()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var token = BuildToken(tid: null, issuer: "https://login.microsoftonline.com/common/v2.0");
        var act = () => sut.Validate(EntraProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_Entra_WithEmptyAllowedTenantIds_Throws()
    {
        // The entity invariant prevents creating an Entra row with empty
        // AllowedTenantIds, but the validator defends in depth in case a row
        // got there some other way (manual SQL, migration bug). useReflectionBypass
        // skips Create's invariant so we can simulate the malformed row.
        SetupRegistry(CreateEntraProvider(
            allowedTenantIds: Array.Empty<string>(), useReflectionBypass: true));

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_Entra_WithNullAllowedTenantIds_Throws()
    {
        SetupRegistry(CreateEntraProvider(
            allowedTenantIds: null, useReflectionBypass: true));

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, BuildToken(tid: AllowedTenantId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // --- GenericOidc happy and unhappy paths ---

    [Fact]
    public async Task Validate_GenericOidc_WithMatchingIssuer_ReturnsPrincipal()
    {
        SetupRegistry(CreateGenericProvider());

        var sut = CreateSut();
        // Issuer must match the OIDC discovery doc's "issuer" field (GenericAuthority).
        var token = BuildToken(tid: null, issuer: GenericAuthority, audience: TestAudience);

        var principal = await sut.Validate(GenericProviderName, token, CancellationToken.None);

        principal.Should().NotBeNull();
        principal.FindFirstValue("sub").Should().Be("test-subject");
    }

    [Fact]
    public async Task Validate_GenericOidc_WithMismatchedIssuer_Throws()
    {
        // A token signed by the right key but claiming a different issuer must
        // be rejected. Without this check a malicious provider with a stolen
        // audience could slip through.
        SetupRegistry(CreateGenericProvider());

        var sut = CreateSut();
        var token = BuildToken(tid: null, issuer: "https://evil.example.com", audience: TestAudience);
        var act = () => sut.Validate(GenericProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_GenericOidc_DoesNotEnforceTenantAllowlist()
    {
        // Even if AllowedTenantIds is present on a GenericOidc row (operator
        // mistake — the entity allows it, just doesn't use it), the validator
        // ignores it. Tenant logic is Entra-specific.
        SetupRegistry(CreateGenericProvider(allowedTenantIds: new[] { AllowedTenantId }));

        var sut = CreateSut();
        // No tid claim at all; issuer matches; token is otherwise valid.
        var token = BuildToken(tid: null, issuer: GenericAuthority, audience: TestAudience);

        var principal = await sut.Validate(GenericProviderName, token, CancellationToken.None);

        principal.Should().NotBeNull();
    }

    // --- Cross-cutting validation checks (provider-type-agnostic) ---

    [Fact]
    public async Task Validate_WithWrongAudience_Throws()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var token = BuildToken(tid: AllowedTenantId, audience: "api://some-other-app");
        var act = () => sut.Validate(EntraProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithExpiredToken_Throws()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var token = BuildToken(tid: AllowedTenantId, expires: DateTime.UtcNow.AddHours(-1));
        var act = () => sut.Validate(EntraProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithDifferentSigningKey_Throws()
    {
        // Token signed by a key NOT in our JWKS — classic stolen-token-from-
        // another-issuer scenario.
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = "other-key" };
        var otherCreds = new SigningCredentials(otherKey, SecurityAlgorithms.RsaSha256);

        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var token = BuildToken(tid: AllowedTenantId, signingCredentials: otherCreds);
        var act = () => sut.Validate(EntraProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithMalformedToken_Throws()
    {
        SetupRegistry(CreateEntraProvider());

        var sut = CreateSut();
        var act = () => sut.Validate(EntraProviderName, "not.a.jwt", CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Validate_WithHs256Token_Throws()
    {
        // Algorithm confusion: an attacker constructs an HS256 token using the
        // (public) RSA key as the HMAC secret. Without ValidAlgorithms pinning
        // some libraries would accept it. We reject explicitly.
        SetupRegistry(CreateEntraProvider());

        // Build an HS256-signed token using a symmetric key derived from
        // the RSA public modulus bytes (just to mimic the attack shape).
        var bytes = _signingKey.Rsa.ExportSubjectPublicKeyInfo();
        var hsKey = new SymmetricSecurityKey(bytes.Take(32).ToArray());
        var hsCreds = new SigningCredentials(hsKey, SecurityAlgorithms.HmacSha256);

        var sut = CreateSut();
        var token = BuildToken(tid: AllowedTenantId, signingCredentials: hsCreds);
        var act = () => sut.Validate(EntraProviderName, token, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    // --- Harness ---

    private OidcTokenValidator CreateSut() =>
        new(_mockRegistry.Object, _mockConfigManagerFactory.Object, _logger);

    private void SetupRegistry(OidcProvider provider)
    {
        _mockRegistry
            .Setup(r => r.GetByName(provider.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
    }

    private static OidcProvider CreateEntraProvider(
        bool isEnabled = true,
        IReadOnlyList<string>? allowedTenantIds = null,
        bool useReflectionBypass = false)
    {
        // The reflection bypass simulates a row that landed in the DB without
        // satisfying the Entra entity invariant — used by the defense-in-depth
        // tests that assert the validator rejects empty/null AllowedTenantIds
        // even if a malformed row somehow exists.
        if (useReflectionBypass)
        {
            return CreateProviderViaReflection(
                name: EntraProviderName,
                providerType: OidcProviderType.MicrosoftEntraId,
                authority: EntraAuthority,
                audience: TestAudience,
                allowedTenantIds: allowedTenantIds,
                isEnabled: isEnabled);
        }

        // Default to a single-tenant allowlist with the test tenant — covers the
        // common happy-path case. Tests that want a different list pass it in.
        allowedTenantIds ??= new[] { AllowedTenantId };

        // The validator pins on authority/audience/client-id, so set them explicitly
        // rather than taking the faker's randomized values.
        var faker = new OidcProviderFaker()
            .WithName(EntraProviderName)
            .WithDisplayName("Microsoft Entra ID")
            .AsMicrosoftEntraId([.. allowedTenantIds])
            .WithAuthority(EntraAuthority)
            .WithClientId("test-client-id")
            .WithAudience(TestAudience);

        return (isEnabled ? faker.AsEnabled() : faker.AsDisabled()).Generate();
    }

    private static OidcProvider CreateGenericProvider(
        IReadOnlyList<string>? allowedTenantIds = null)
    {
        var provider = new OidcProviderFaker()
            .WithName(GenericProviderName)
            .WithDisplayName("Acme Google")
            .AsGenericOidc()
            .WithAuthority(GenericAuthority)
            .WithClientId("test-client-id")
            .WithAudience(TestAudience)
            .Generate();

        // GenericOidc ignores the tenant allowlist, but a couple of tests assert the
        // validator does too — honor an explicitly supplied list via field binding.
        if (allowedTenantIds is not null)
        {
            typeof(OidcProvider).GetProperty(nameof(OidcProvider.AllowedTenantIds))!
                .SetValue(provider, allowedTenantIds);
        }

        return provider;
    }

    /// <summary>
    /// Builds an OidcProvider via the parameterless ctor + reflection on the
    /// init/private property setters. Needed only for tests that exercise the
    /// validator's "row landed in DB without invariants" defense path — Create
    /// itself rejects that combination.
    /// </summary>
    private static OidcProvider CreateProviderViaReflection(
        string name,
        OidcProviderType providerType,
        string authority,
        string audience,
        IReadOnlyList<string>? allowedTenantIds,
        bool isEnabled)
    {
        var provider = (OidcProvider)Activator.CreateInstance(typeof(OidcProvider), nonPublic: true)!;
        SetProp(provider, nameof(OidcProvider.Name), name);
        SetProp(provider, nameof(OidcProvider.DisplayName), "Test");
        SetProp(provider, nameof(OidcProvider.ProviderType), providerType);
        SetProp(provider, nameof(OidcProvider.Authority), authority);
        SetProp(provider, nameof(OidcProvider.ClientId), "test-client-id");
        SetProp(provider, nameof(OidcProvider.Audience), audience);
        SetProp(provider, nameof(OidcProvider.Scopes), (IReadOnlyList<string>)new[] { "openid" });
        SetProp(provider, nameof(OidcProvider.AllowedTenantIds), allowedTenantIds);
        SetProp(provider, nameof(OidcProvider.ClockSkewSeconds), 60);
        SetProp(provider, nameof(OidcProvider.IsEnabled), isEnabled);
        return provider;
    }

    private static void SetProp(object target, string name, object? value)
    {
        var prop = target.GetType().GetProperty(name)!;
        prop.SetValue(target, value);
    }

    private string BuildToken(
        string? tid,
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null,
        SigningCredentials? signingCredentials = null)
    {
        var claims = new List<Claim> { new("sub", "test-subject") };
        if (!string.IsNullOrEmpty(tid))
        {
            claims.Add(new Claim("tid", tid));
        }

        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);
        var notBefore = expiresAt < DateTime.UtcNow
            ? expiresAt.AddMinutes(-5)
            : DateTime.UtcNow.AddMinutes(-5);

        var handler = new JwtSecurityTokenHandler();
        handler.OutboundClaimTypeMap.Clear();
        var jwt = handler.CreateJwtSecurityToken(
            issuer: issuer ?? $"https://login.microsoftonline.com/{AllowedTenantId}/v2.0",
            audience: audience ?? TestAudience,
            subject: new ClaimsIdentity(claims),
            notBefore: notBefore,
            expires: expiresAt,
            issuedAt: notBefore,
            signingCredentials: signingCredentials ?? _signingCredentials);

        return handler.WriteToken(jwt);
    }
}
