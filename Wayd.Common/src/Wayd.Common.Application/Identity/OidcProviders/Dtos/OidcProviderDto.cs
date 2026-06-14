namespace Wayd.Common.Application.Identity.OidcProviders.Dtos;

/// <summary>
/// Admin-facing detail DTO. Includes every editable field plus audit metadata.
/// </summary>
public sealed record OidcProviderDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string ProviderType { get; set; }
    public required string Authority { get; set; }
    public required string ClientId { get; set; }
    public required string Audience { get; set; }
    public IReadOnlyList<string> Scopes { get; set; } = [];
    public IReadOnlyList<string>? AllowedTenantIds { get; set; }
    public int ClockSkewSeconds { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether first-time sign-ins through this provider auto-create an account.
    /// </summary>
    public bool AllowAutoRegistration { get; set; }

    /// <summary>
    /// Whether auto-registration is restricted to users with a matching employee
    /// record. Only meaningful when <see cref="AllowAutoRegistration"/> is true.
    /// </summary>
    public bool? RequireEmployeeRecord { get; set; }

    /// <summary>
    /// Role id assigned to auto-created users. Null when auto-registration is
    /// disabled (a default role is required whenever it's enabled).
    /// </summary>
    public string? DefaultRoleId { get; set; }
}
