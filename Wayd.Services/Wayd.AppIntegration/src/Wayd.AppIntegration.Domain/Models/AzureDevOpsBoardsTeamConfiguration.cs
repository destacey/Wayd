using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Wayd.AppIntegration.Domain.Models;

public sealed class AzureDevOpsBoardsTeamConfiguration
{
    [JsonConstructor]
    private AzureDevOpsBoardsTeamConfiguration() { }

    [SetsRequiredMembers]
    public AzureDevOpsBoardsTeamConfiguration(IEnumerable<AzureDevOpsBoardsWorkspaceTeam> workspaceTeams)
    {
        WorkspaceTeams = workspaceTeams?.ToList() ?? [];
    }

    /// <summary>
    /// Gets or sets the workspace teams.
    /// </summary>
    [JsonInclude]
    public List<AzureDevOpsBoardsWorkspaceTeam> WorkspaceTeams { get; private set; } = [];

    public Result UpsertWorkspaceTeam(Guid workspaceId, Guid teamId, string teamName, Guid? boardId)
    {
        Guard.Against.Default(workspaceId, nameof(workspaceId));
        Guard.Against.Default(teamId, nameof(teamId));
        Guard.Against.NullOrWhiteSpace(teamName, nameof(teamName));
        Guard.Against.Default(boardId, nameof(boardId));

        var existingTeam = WorkspaceTeams.SingleOrDefault(w => w.WorkspaceId == workspaceId && w.TeamId == teamId);
        if (existingTeam is null)
        {
            WorkspaceTeams.Add(new AzureDevOpsBoardsWorkspaceTeam(workspaceId, teamId, teamName, boardId));
        }
        else
        {
            existingTeam.Update(teamName, boardId);
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes teams from the configuration.
    /// </summary>
    /// <param name="teamIds"></param>
    /// <returns></returns>
    public Result RemoveTeams(IEnumerable<Guid> teamIds)
    {
        if (teamIds is null || !teamIds.Any())
            return Result.Success();

        WorkspaceTeams.RemoveAll(w => teamIds.Contains(w.TeamId));

        return Result.Success();
    }

    /// <summary>
    /// Resets the team configuration for a specific team.  This is a hack because EF/SQL is throwing an exception when we set the InternalTeamId to null.  This seems to happen because the stored procedure paramater value is NULL.  It works when the paramater is bypassed. 
    /// </summary>
    /// <param name="teamId"></param>
    /// <returns></returns>
    public Result ResetTeam(Guid teamId)
    {
        Guard.Against.Default(teamId, nameof(teamId));

        var team = WorkspaceTeams.Single(w => w.TeamId == teamId);
        WorkspaceTeams.Remove(team);

        WorkspaceTeams.Add(new AzureDevOpsBoardsWorkspaceTeam(team.WorkspaceId, team.TeamId, team.TeamName, team.BoardId));

        return Result.Success();
    }

    /// <summary>
    /// Removes teams from the configuration for a specific workspace.
    /// </summary>
    /// <param name="workspaceId"></param>
    /// <returns></returns>
    public Result RemoveTeamsForWorkspace(Guid workspaceId)
    {
        WorkspaceTeams.RemoveAll(w => w.WorkspaceId == workspaceId);

        return Result.Success();
    }

    public static AzureDevOpsBoardsTeamConfiguration CreateEmpty()
    {
        return new AzureDevOpsBoardsTeamConfiguration();
    }
}
