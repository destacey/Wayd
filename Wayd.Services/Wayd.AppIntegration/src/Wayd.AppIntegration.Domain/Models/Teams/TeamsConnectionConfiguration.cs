using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Wayd.Common.Domain.DataProtection;

namespace Wayd.AppIntegration.Domain.Models.Teams;

/// <summary>
/// Microsoft Graph app-registration credentials for a Teams communication connection.
/// </summary>
public sealed class TeamsConnectionConfiguration
{
    [JsonConstructor]
    private TeamsConnectionConfiguration() { }

    [SetsRequiredMembers]
    public TeamsConnectionConfiguration(string tenantId, string clientId, string clientSecret)
    {
        TenantId = tenantId.Trim();
        ClientId = clientId.Trim();
        ClientSecret = clientSecret.Trim();
        ConfigVersion = 1;
    }

    /// <summary>The Entra ID (Azure AD) tenant identifier.</summary>
    public required string TenantId { get; set; }

    /// <summary>The application (client) identifier for the app registration used to call Microsoft Graph.</summary>
    public required string ClientId { get; set; }

    /// <summary>The client secret for the app registration. Encrypted at rest.</summary>
    [Encrypted]
    public required string ClientSecret { get; set; }

    public int ConfigVersion { get; init; }
}
