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

    /// <summary>Which uniquely-indexed Employee field the sync upsert matches on.</summary>
    public EmployeeMatchProperty MatchBy { get; set; }

    /// <summary>
    /// When true, names that come back from Entra in all-caps are title-cased before storage.
    /// </summary>
    public bool NormalizeNameCasing { get; set; }

    /// <summary>
    /// Replaces the ClientSecret with a masked form that preserves the first 4 characters
    /// and the original length. This matches the AzDO PAT masking pattern so the
    /// <c>UpdateEntraConnectionCommand</c> handler can detect "user posted back the masked
    /// value unchanged" by comparing the first 4 characters and length — without that, an
    /// unchanged edit would overwrite the stored secret with the masked placeholder.
    /// </summary>
    public void MaskClientSecret()
    {
        if (!string.IsNullOrWhiteSpace(ClientSecret) && ClientSecret.Length > 4)
            ClientSecret = string.Concat(ClientSecret.AsSpan(0, 4), new string('*', ClientSecret.Length - 4));
    }
}
