using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Entra;

public sealed record EntraConnectionConfigurationDto : IMapFrom<EntraConnectionConfiguration>
{
    /// <summary>
    /// The Entra ID (Azure AD) tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// The application (client) identifier for the Entra app registration used to call Microsoft Graph.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// The client secret for the Entra app registration.
    /// </summary>
    /// <remarks>Masked to a fixed-width placeholder when returned from the API.</remarks>
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Optional Entra group object ID to scope the user query to. When null, all member users in
    /// the tenant are queried.
    /// </summary>
    public string? AllUsersGroupObjectId { get; set; }

    /// <summary>
    /// When true, users with disabled accounts are also included in the sync.
    /// </summary>
    public bool IncludeDisabledUsers { get; set; }

    /// <summary>Which uniquely-indexed Employee field the sync upsert matches on.</summary>
    public EmployeeMatchProperty MatchBy { get; set; }

    /// <summary>
    /// When true, names that come back from Entra in all-caps are title-cased before storage.
    /// </summary>
    public bool NormalizeNameCasing { get; set; }

    /// <summary>Replaces the client secret with the fixed-width placeholder. See <see cref="ConnectionSecret"/>.</summary>
    public void MaskClientSecret()
        => ClientSecret = ConnectionSecret.Masked(ClientSecret);
}
