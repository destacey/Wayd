using Wayd.Common.Domain.Identity;

namespace Wayd.Web.Api.Models.UserManagement.OidcProviders;

public sealed record CreateOidcProviderRequest(
    string Name,
    string DisplayName,
    OidcProviderType ProviderType,
    string Authority,
    string ClientId,
    string Audience,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string>? AllowedTenantIds,
    int ClockSkewSeconds,
    bool IsEnabled,
    // Defaults preserve the historical behavior (auto-register employee-matched
    // users into Basic) for callers that omit the registration-policy fields.
    bool AllowAutoRegistration = true,
    bool RequireEmployeeRecord = true,
    string? DefaultRoleId = null);

public sealed record UpdateOidcProviderRequest(
    Guid Id,
    string DisplayName,
    string Authority,
    string ClientId,
    string Audience,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string>? AllowedTenantIds,
    int ClockSkewSeconds,
    bool IsEnabled,
    bool AllowAutoRegistration = true,
    bool RequireEmployeeRecord = true,
    string? DefaultRoleId = null);
