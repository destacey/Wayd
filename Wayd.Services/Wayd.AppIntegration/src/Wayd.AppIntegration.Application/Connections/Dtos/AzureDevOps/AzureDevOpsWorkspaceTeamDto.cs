using Mapster;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

public sealed record AzureDevOpsWorkspaceTeamDto : IMapFrom<AzureDevOpsBoardsWorkspaceTeam>
{
    /// <summary>
    /// The unique identifier for the workspace in the Azure DevOps system.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The unique identifier for the team in the Azure DevOps system.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// The name of the team in the Azure DevOps system.
    /// </summary>
    public required string TeamName { get; set; }

    /// <summary>
    /// The unique identifier for the board in the Azure DevOps system.
    /// </summary>
    public Guid? BoardId { get; set; }

    /// <summary>
    /// The id of the Wayd team mapped to this Azure DevOps team; null when the team is not mapped.
    /// </summary>
    public Guid? InternalTeamId { get; set; }
}
