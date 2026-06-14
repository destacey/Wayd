using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.Common.Domain.Data;
using NodaTime;

namespace Wayd.Common.Domain.Identity;

/// <summary>
/// The kind of OIDC provider. Drives validation rules and how tokens are matched
/// to <c>UserIdentity</c> rows. The two kinds we support today:
/// <list type="bullet">
///   <item><see cref="MicrosoftEntraId"/> — Microsoft Entra ID. Multi-tenant, validates
///   the <c>tid</c> claim against <see cref="OidcProvider.AllowedTenantIds"/>.</item>
///   <item><see cref="GenericOidc"/> — any other standards-compliant provider
///   (Google, Okta, Auth0, Keycloak, …). Single-tenant per provider row.</item>
/// </list>
/// </summary>
public enum OidcProviderType
{
    MicrosoftEntraId = 0,
    GenericOidc = 1,
}

/// <summary>
/// Configuration for an OIDC provider that users can sign in with. One row per
/// configured provider; admins manage these through the Settings UI.
/// </summary>
/// <remarks>
/// No client secret is stored. The frontend uses PKCE for public-client flows,
/// and the backend validates incoming subject tokens against the provider's
/// JWKS (no secret required). If a future feature needs a server-side secret
/// (e.g. authorization-code flow, M2M, refresh-token rotation), an encrypted
/// column can be added with proper Data Protection wiring at that point.
/// </remarks>
public sealed class OidcProvider : BaseAuditableEntity
{
    private OidcProvider() { }

    private OidcProvider(
        string name,
        string displayName,
        OidcProviderType providerType,
        string authority,
        string clientId,
        string audience,
        IReadOnlyList<string> scopes,
        IReadOnlyList<string>? allowedTenantIds,
        int clockSkewSeconds,
        bool isEnabled,
        RegistrationPolicy registrationPolicy)
    {
        Name = name;
        DisplayName = displayName;
        ProviderType = providerType;
        Authority = authority;
        ClientId = clientId;
        Audience = audience;
        Scopes = scopes;
        AllowedTenantIds = allowedTenantIds;
        ClockSkewSeconds = clockSkewSeconds;
        IsEnabled = isEnabled;
        RegistrationPolicy = registrationPolicy;
    }

    /// <summary>
    /// Stable provider key. Stored on <c>UserIdentity.Provider</c> for every
    /// account that signed in through this provider — changing it would orphan
    /// those rows, so the entity rejects renames once it exists.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = null!;

    /// <summary>
    /// Human-readable label shown on the login page (e.g. "Acme Okta").
    /// </summary>
    public string DisplayName
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(DisplayName)).Trim();
    } = null!;

    public OidcProviderType ProviderType { get; private set; }

    /// <summary>
    /// OIDC issuer base URL. The validator appends <c>/.well-known/openid-configuration</c>
    /// for discovery. Must be HTTPS.
    /// </summary>
    public string Authority
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Authority)).Trim();
    } = null!;

    /// <summary>
    /// Public OAuth client ID issued by the provider for this Wayd deployment.
    /// </summary>
    public string ClientId
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(ClientId)).Trim();
    } = null!;

    /// <summary>
    /// Pinned <c>aud</c> claim value. For Entra v2 tokens this is typically the
    /// client ID; for v1 tokens it's <c>api://&lt;clientId&gt;</c>. Different per
    /// provider — must match what's in the actual token.
    /// </summary>
    public string Audience
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Audience)).Trim();
    } = null!;

    /// <summary>
    /// OAuth scopes the frontend requests when getting a token from this
    /// provider. Backend doesn't enforce these (it only validates the token);
    /// they're delivered to the frontend via <c>/api/auth/providers</c>.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; private set; } = [];

    /// <summary>
    /// Tenant allowlist enforced by the token validator. Required for
    /// <see cref="OidcProviderType.MicrosoftEntraId"/>; ignored for
    /// <see cref="OidcProviderType.GenericOidc"/>. An empty list for an Entra
    /// provider would reject every login, so the factory rejects it.
    /// </summary>
    public IReadOnlyList<string>? AllowedTenantIds { get; private set; }

    public int ClockSkewSeconds { get; private set; } = 60;

    /// <summary>
    /// Disabled providers are hidden from the login page and reject token
    /// exchanges. Lets an admin take a provider out of service without losing
    /// its configuration or breaking the <c>UserIdentity</c> rows that point at it.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Just-in-time provisioning policy: whether first-time sign-ins auto-create an
    /// account and, if so, under what employee-record gate and default role. The
    /// dependent settings are only readable while auto-registration is enabled — see
    /// <see cref="Identity.RegistrationPolicy"/>. Never <c>null</c>; a brand-new or
    /// reset provider carries a disabled policy.
    /// </summary>
    public RegistrationPolicy RegistrationPolicy { get; private set; } = RegistrationPolicy.Disabled();

    /// <summary>
    /// Creates a new OIDC provider configuration. Validates the type-specific
    /// invariants up front (e.g. Entra requires at least one tenant ID).
    /// </summary>
    /// <param name="name">The name of the provider.</param>
    /// <param name="displayName">The display name of the provider.</param>
    /// <param name="providerType">The type of the provider.</param>
    /// <param name="authority">The authority URL of the provider.</param>
    /// <param name="clientId">The client ID of the provider.</param>
    /// <param name="audience">The audience of the provider.</param>
    /// <param name="scopes">The scopes of the provider.</param>
    /// <param name="allowedTenantIds">The allowed tenant IDs of the provider.</param>
    /// <param name="clockSkewSeconds">The clock skew in seconds.</param>
    /// <param name="isEnabled">Whether the provider is enabled.</param>
    /// <param name="timestamp">The timestamp of the creation.</param>
    /// <param name="registrationPolicy">The registration policy of the provider. Optional; if not provided, defaults to disabled.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static Result<OidcProvider> Create(
        string name,
        string displayName,
        OidcProviderType providerType,
        string authority,
        string clientId,
        string audience,
        IReadOnlyList<string> scopes,
        IReadOnlyList<string>? allowedTenantIds,
        int clockSkewSeconds,
        bool isEnabled,
        Instant timestamp,
        RegistrationPolicy? registrationPolicy = null)
    {
        try
        {
            var validation = ValidateInvariants(name, authority, providerType, allowedTenantIds, clockSkewSeconds);
            if (validation.IsFailure)
            {
                return Result.Failure<OidcProvider>(validation.Error);
            }

            var provider = new OidcProvider(
                name,
                displayName,
                providerType,
                authority,
                clientId,
                audience,
                scopes ?? [],
                NormalizeAllowedTenantIds(allowedTenantIds),
                clockSkewSeconds,
                isEnabled,
                registrationPolicy ?? RegistrationPolicy.Disabled());

            provider.AddDomainEvent(EntityCreatedEvent.WithEntity(provider, timestamp));

            return Result.Success(provider);
        }
        catch (Exception ex)
        {
            return Result.Failure<OidcProvider>(ex.Message);
        }
    }

    /// <summary>
    /// Updates editable fields. <see cref="Name"/> and <see cref="ProviderType"/>
    /// are intentionally not updatable — Name is the foreign key written into
    /// <c>UserIdentity.Provider</c>, and ProviderType affects which validator
    /// path applies to existing identities. Mutating either would orphan or
    /// silently re-route those rows.
    /// </summary>
    /// <param name="displayName">The display name of the provider.</param>
    /// <param name="authority">The authority URL of the provider.</param>
    /// <param name="clientId">The client ID of the provider.</param>
    /// <param name="audience">The audience of the provider.</param>
    /// <param name="scopes">The scopes of the provider.</param>
    /// <param name="allowedTenantIds">The allowed tenant IDs of the provider.</param>
    /// <param name="clockSkewSeconds">The clock skew in seconds.</param>
    /// <param name="isEnabled">Whether the provider is enabled.</param>
    /// <param name="timestamp">The timestamp of the update.</param>
    /// <param name="registrationPolicy">The registration policy of the provider. Optional; if not provided, defaults to disabled.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result Update(
        string displayName,
        string authority,
        string clientId,
        string audience,
        IReadOnlyList<string> scopes,
        IReadOnlyList<string>? allowedTenantIds,
        int clockSkewSeconds,
        bool isEnabled,
        Instant timestamp,
        RegistrationPolicy? registrationPolicy = null)
    {
        try
        {
            var validation = ValidateInvariants(Name, authority, ProviderType, allowedTenantIds, clockSkewSeconds);
            if (validation.IsFailure)
            {
                return Result.Failure(validation.Error);
            }

            DisplayName = displayName;
            Authority = authority;
            ClientId = clientId;
            Audience = audience;
            Scopes = scopes ?? [];
            AllowedTenantIds = NormalizeAllowedTenantIds(allowedTenantIds);
            ClockSkewSeconds = clockSkewSeconds;
            IsEnabled = isEnabled;
            RegistrationPolicy = registrationPolicy ?? RegistrationPolicy.Disabled();

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Convenience for the test-connection / disable / enable admin actions
    /// without rewriting the whole record. Emits an update event so audit
    /// captures who flipped the switch.
    /// </summary>
    public Result SetEnabled(bool isEnabled, Instant timestamp)
    {
        if (IsEnabled == isEnabled) return Result.Success();

        IsEnabled = isEnabled;
        AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));
        return Result.Success();
    }

    private static Result ValidateInvariants(
        string name,
        string authority,
        OidcProviderType providerType,
        IReadOnlyList<string>? allowedTenantIds,
        int clockSkewSeconds)
    {
        if (string.IsNullOrWhiteSpace(authority) ||
            !Uri.TryCreate(authority, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Authority must be an absolute HTTPS URL.");
        }

        if (providerType == OidcProviderType.MicrosoftEntraId)
        {
            var hasTenant = allowedTenantIds is not null
                && allowedTenantIds.Any(t => !string.IsNullOrWhiteSpace(t));
            if (!hasTenant)
            {
                return Result.Failure(
                    "Microsoft Entra ID providers require at least one AllowedTenantId.");
            }
        }

        if (clockSkewSeconds < 0 || clockSkewSeconds > 600)
        {
            return Result.Failure("ClockSkewSeconds must be between 0 and 600.");
        }

        // The "Wayd" name is reserved for the local-account identity in UserIdentity
        // rows. Allowing a row named "Wayd" would let an OIDC provider impersonate
        // local accounts when the registry looks up identities.
        if (string.Equals(name?.Trim(), "Wayd", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("'Wayd' is a reserved provider name.");
        }

        return Result.Success();
    }

    private static IReadOnlyList<string>? NormalizeAllowedTenantIds(IReadOnlyList<string>? values)
    {
        if (values is null) return null;
        var cleaned = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
