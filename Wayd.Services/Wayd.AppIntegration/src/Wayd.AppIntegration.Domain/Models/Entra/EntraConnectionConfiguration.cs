using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Wayd.Common.Domain.DataProtection;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Domain.Models.Entra;

public sealed class EntraConnectionConfiguration
{
    [JsonConstructor]
    private EntraConnectionConfiguration() { }

    [SetsRequiredMembers]
    public EntraConnectionConfiguration(
        string tenantId,
        string clientId,
        string clientSecret,
        string? allUsersGroupObjectId = null,
        bool includeDisabledUsers = false,
        EmployeeMatchProperty matchBy = EmployeeMatchProperty.Email,
        bool normalizeNameCasing = true)
    {
        TenantId = tenantId.Trim();
        ClientId = clientId.Trim();
        ClientSecret = clientSecret.Trim();
        AllUsersGroupObjectId = string.IsNullOrWhiteSpace(allUsersGroupObjectId) ? null : allUsersGroupObjectId.Trim();
        IncludeDisabledUsers = includeDisabledUsers;
        MatchBy = matchBy;
        NormalizeNameCasing = normalizeNameCasing;
        ConfigVersion = 3;
    }

    /// <summary>
    /// The Entra ID (Azure AD) tenant identifier.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// The application (client) identifier for the Entra app registration used to call Microsoft Graph.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// The client secret for the Entra app registration. Encrypted at rest.
    /// </summary>
    [Encrypted]
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Optional Entra group object ID to scope the user query to. When null, all member users in
    /// the tenant are queried.
    /// </summary>
    public string? AllUsersGroupObjectId { get; set; }

    /// <summary>
    /// When true, users with disabled accounts are also included in the sync. Defaults to false.
    /// </summary>
    public bool IncludeDisabledUsers { get; set; }

    /// <summary>
    /// Which uniquely-indexed field on <c>Employee</c> the upsert matches on. Defaults to email,
    /// which lets a tenant later swap to a different PeopleSync connector without forking the
    /// employee dataset.
    /// </summary>
    public EmployeeMatchProperty MatchBy { get; set; }

    /// <summary>
    /// When true, names that come back from Entra in all-caps are title-cased before storage.
    /// Mixed-case input is preserved. Default true.
    /// </summary>
    public bool NormalizeNameCasing { get; set; }

    public int ConfigVersion { get; init; }
}
