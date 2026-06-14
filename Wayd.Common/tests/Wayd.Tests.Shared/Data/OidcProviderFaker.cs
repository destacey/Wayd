using Wayd.Common.Domain.Identity;

namespace Wayd.Tests.Shared.Data;

/// <summary>
/// Produces valid <see cref="OidcProvider"/> instances for tests. Defaults to a
/// GenericOidc provider (no tenant allowlist required) with auto-registration
/// disabled — the secure default. Use the <c>As*</c>/<c>With*</c> extensions to
/// shape it (e.g. <see cref="OidcProviderFakerExtensions.AsMicrosoftEntraId"/> or
/// <see cref="OidcProviderFakerExtensions.WithAutoRegistration"/>).
/// </summary>
public sealed class OidcProviderFaker : PrivateConstructorFaker<OidcProvider>
{
    public OidcProviderFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Internet.DomainWord().ToLowerInvariant() + "-oidc");
        RuleFor(x => x.DisplayName, f => $"{f.Company.CompanyName()} SSO");
        RuleFor(x => x.ProviderType, OidcProviderType.GenericOidc);
        RuleFor(x => x.Authority, f => $"https://{f.Internet.DomainName()}");
        RuleFor(x => x.ClientId, f => f.Random.Guid().ToString());
        RuleFor(x => x.Audience, f => f.Random.Guid().ToString());
        RuleFor(x => x.Scopes, _ => new[] { "openid", "profile", "email" });
        // GenericOidc ignores the tenant allowlist; AsMicrosoftEntraId() supplies one.
        RuleFor(x => x.AllowedTenantIds, (IReadOnlyList<string>?)null);
        RuleFor(x => x.ClockSkewSeconds, 60);
        RuleFor(x => x.IsEnabled, true);
        // Secure by default, mirroring the entity's own default.
        RuleFor(x => x.RegistrationPolicy, _ => RegistrationPolicy.Disabled());
    }
}

public static class OidcProviderFakerExtensions
{
    public static OidcProviderFaker WithId(this OidcProviderFaker faker, Guid id)
    {
        faker.RuleFor(x => x.Id, id);
        return faker;
    }

    public static OidcProviderFaker WithName(this OidcProviderFaker faker, string name)
    {
        faker.RuleFor(x => x.Name, name);
        return faker;
    }

    public static OidcProviderFaker WithDisplayName(this OidcProviderFaker faker, string displayName)
    {
        faker.RuleFor(x => x.DisplayName, displayName);
        return faker;
    }

    public static OidcProviderFaker WithAuthority(this OidcProviderFaker faker, string authority)
    {
        faker.RuleFor(x => x.Authority, authority);
        return faker;
    }

    public static OidcProviderFaker WithClientId(this OidcProviderFaker faker, string clientId)
    {
        faker.RuleFor(x => x.ClientId, clientId);
        return faker;
    }

    public static OidcProviderFaker WithAudience(this OidcProviderFaker faker, string audience)
    {
        faker.RuleFor(x => x.Audience, audience);
        return faker;
    }

    public static OidcProviderFaker WithScopes(this OidcProviderFaker faker, params string[] scopes)
    {
        faker.RuleFor(x => x.Scopes, scopes);
        return faker;
    }

    public static OidcProviderFaker WithClockSkewSeconds(this OidcProviderFaker faker, int clockSkewSeconds)
    {
        faker.RuleFor(x => x.ClockSkewSeconds, clockSkewSeconds);
        return faker;
    }

    /// <summary>Turns this into a Microsoft Entra ID provider with a tenant allowlist (required for that type).</summary>
    public static OidcProviderFaker AsMicrosoftEntraId(this OidcProviderFaker faker, params string[] allowedTenantIds)
    {
        faker.RuleFor(x => x.ProviderType, OidcProviderType.MicrosoftEntraId);
        faker.RuleFor(
            x => x.AllowedTenantIds,
            f => (IReadOnlyList<string>)(allowedTenantIds.Length > 0
                ? allowedTenantIds
                : new[] { f.Random.Guid().ToString() }));
        return faker;
    }

    /// <summary>Turns this into a GenericOidc provider (no tenant allowlist).</summary>
    public static OidcProviderFaker AsGenericOidc(this OidcProviderFaker faker)
    {
        faker.RuleFor(x => x.ProviderType, OidcProviderType.GenericOidc);
        faker.RuleFor(x => x.AllowedTenantIds, (IReadOnlyList<string>?)null);
        return faker;
    }

    public static OidcProviderFaker AsEnabled(this OidcProviderFaker faker)
    {
        faker.RuleFor(x => x.IsEnabled, true);
        return faker;
    }

    public static OidcProviderFaker AsDisabled(this OidcProviderFaker faker)
    {
        faker.RuleFor(x => x.IsEnabled, false);
        return faker;
    }

    /// <summary>
    /// Enables auto-registration with an employee-record gate and a required default
    /// role. A role id is generated when none is supplied, since an enabled policy
    /// must always name one.
    /// </summary>
    public static OidcProviderFaker WithAutoRegistration(
        this OidcProviderFaker faker,
        bool requireEmployeeRecord = true,
        string? defaultRoleId = null)
    {
        faker.RuleFor(
            x => x.RegistrationPolicy,
            f => RegistrationPolicy.Enabled(requireEmployeeRecord, defaultRoleId ?? f.Random.Guid().ToString()));
        return faker;
    }

    /// <summary>Disables auto-registration (the secure default).</summary>
    public static OidcProviderFaker WithoutAutoRegistration(this OidcProviderFaker faker)
    {
        faker.RuleFor(x => x.RegistrationPolicy, _ => RegistrationPolicy.Disabled());
        return faker;
    }
}
