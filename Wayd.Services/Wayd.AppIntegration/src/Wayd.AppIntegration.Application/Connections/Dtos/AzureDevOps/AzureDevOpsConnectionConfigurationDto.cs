using Mapster;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

public sealed record AzureDevOpsConnectionConfigurationDto : IMapFrom<AzureDevOpsBoardsConnectionConfiguration>
{
    /// <summary>Gets the organization.</summary>
    /// <value>The Azure DevOps Organization name.</value>
    public required string Organization { get; set; }

    /// <summary>Gets the personal access token.</summary>
    /// <value>The personal access token that enables access to Azure DevOps data.</value>
    /// <remarks>Masked to a fixed-width placeholder when returned from the API.</remarks>
    public required string PersonalAccessToken { get; set; }

    /// <summary>Gets the organization URL.</summary>
    /// <value>The organization URL.</value>
    public required string OrganizationUrl { get; set; }

    /// <summary>
    /// Gets or sets the work processes.
    /// </summary>
    public required List<AzureDevOpsWorkProcessDto> WorkProcesses { get; set; }

    /// <summary>
    /// Gets or sets the workspaces.
    /// </summary>
    public required List<AzureDevOpsWorkspaceDto> Workspaces { get; set; }

    /// <summary>Replaces the token with the fixed-width placeholder. See <see cref="ConnectionSecret"/>.</summary>
    public void MaskPersonalAccessToken()
        => PersonalAccessToken = ConnectionSecret.Masked(PersonalAccessToken);
}
