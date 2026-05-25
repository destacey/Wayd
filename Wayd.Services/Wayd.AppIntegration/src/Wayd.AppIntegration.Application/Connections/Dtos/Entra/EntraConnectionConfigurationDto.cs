using Wayd.AppIntegration.Domain.Models.Entra;

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
    /// <remarks>This will be masked when returned from the API.</remarks>
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

    /// <summary>
    /// Replaces the ClientSecret with a fixed masked placeholder.
    /// </summary>
    public void MaskClientSecret()
    {
        ClientSecret = "***MASKED***";
    }
}
