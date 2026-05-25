namespace Wayd.Common.Application.Interfaces.ExternalPeople;

/// <summary>
/// Per-connection credentials used to build a transient <see cref="Microsoft.Graph.GraphServiceClient"/>
/// when syncing people from a specific Entra tenant. Carries everything the source needs to
/// authenticate and shape the query — there is no app-wide configuration fallback.
/// </summary>
public sealed record EntraConnectionCredentials(
    string TenantId,
    string ClientId,
    string ClientSecret,
    string? AllUsersGroupObjectId,
    bool IncludeDisabledUsers);
