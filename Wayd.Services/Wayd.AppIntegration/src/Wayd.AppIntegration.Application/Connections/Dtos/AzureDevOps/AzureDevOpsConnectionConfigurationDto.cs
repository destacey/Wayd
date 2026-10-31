using Mapster;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

public sealed record AzureDevOpsConnectionConfigurationDto : IMapFrom<AzureDevOpsBoardsConnectionConfiguration>
{
    /// <summary>The Azure DevOps organization name.</summary>
    public required string Organization { get; set; }

    /// <summary>
    /// The personal access token that grants access to the organization's Azure DevOps data.
    /// Returned masked as a fixed-width placeholder.
    /// </summary>
    public required string PersonalAccessToken { get; set; }

    /// <summary>
    /// The Azure DevOps organization URL, built from the organization name and the configured
    /// Azure DevOps base address.
    /// </summary>
    public required string OrganizationUrl { get; set; }

    /// <summary>Sets <see cref="OrganizationUrl"/>, which no stored field holds.</summary>
    public void ResolveOrganizationUrl(IAzureDevOpsUrlResolver urlResolver)
        => OrganizationUrl = urlResolver.GetOrganizationUrl(Organization);

    /// <summary>
    /// The Azure DevOps work processes discovered for the organization.
    /// </summary>
    public required List<AzureDevOpsWorkProcessDto> WorkProcesses { get; set; }

    /// <summary>
    /// The Azure DevOps projects discovered for the organization; each can be integrated as a Wayd workspace.
    /// </summary>
    public required List<AzureDevOpsWorkspaceDto> Workspaces { get; set; }

    /// <summary>Replaces the token with the fixed-width placeholder. See <see cref="ConnectionSecret"/>.</summary>
    public void MaskPersonalAccessToken()
        => PersonalAccessToken = ConnectionSecret.Masked(PersonalAccessToken);
}
