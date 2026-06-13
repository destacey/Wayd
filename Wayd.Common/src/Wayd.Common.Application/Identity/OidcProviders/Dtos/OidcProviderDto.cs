namespace Wayd.Common.Application.Identity.OidcProviders.Dtos;

/// <summary>
/// Admin-facing detail DTO. Includes every editable field plus audit metadata.
/// </summary>
public sealed record OidcProviderDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string ProviderType { get; init; } = null!;
    public string Authority { get; init; } = null!;
    public string ClientId { get; init; } = null!;
    public string Audience { get; init; } = null!;
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public IReadOnlyList<string>? AllowedTenantIds { get; init; }
    public int ClockSkewSeconds { get; init; }
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether first-time sign-ins through this provider auto-create an account.
    /// </summary>
    public bool AllowAutoRegistration { get; init; }

    /// <summary>
    /// Whether auto-registration is restricted to users with a matching employee
    /// record. Only meaningful when <see cref="AllowAutoRegistration"/> is true.
    /// </summary>
    public bool RequireEmployeeRecord { get; init; }

    /// <summary>
    /// Role id assigned to auto-created users; null means the built-in Basic role.
    /// The admin UI resolves the display name from its roles list.
    /// </summary>
    public string? DefaultRoleId { get; init; }
}

/// <summary>
/// Compact list row. Same fields as the detail DTO; kept as a separate type so
/// the API contract is explicit and the listing endpoint stays stable if the
/// detail shape grows fields later.
/// </summary>
public sealed record OidcProviderListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public string ProviderType { get; init; } = null!;
    public string Authority { get; init; } = null!;
    public bool IsEnabled { get; init; }
}
