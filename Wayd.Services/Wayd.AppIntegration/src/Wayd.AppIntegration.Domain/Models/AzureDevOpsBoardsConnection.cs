using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Extensions;

namespace Wayd.AppIntegration.Domain.Models;

public sealed class AzureDevOpsBoardsConnection : Connection<AzureDevOpsBoardsConnectionConfiguration>, ISyncableConnection
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private AzureDevOpsBoardsConnection() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private AzureDevOpsBoardsConnection(string name, string? description, string? systemId, AzureDevOpsBoardsConnectionConfiguration configuration, bool configurationIsValid, AzureDevOpsBoardsTeamConfiguration? teamConfiguration)
    {
        Name = name;
        Description = description;
        SystemId = systemId;
        Connector = Connector.AzureDevOps;
        Configuration = Guard.Against.Null(configuration, nameof(Configuration));
        IsValidConfiguration = configurationIsValid;
        TeamConfiguration = teamConfiguration ?? AzureDevOpsBoardsTeamConfiguration.CreateEmpty();
    }

    public override AzureDevOpsBoardsConnectionConfiguration Configuration { get; protected set; }

    public AzureDevOpsBoardsTeamConfiguration TeamConfiguration { get; private set; }

    // ISyncableConnection implementation
    public string? SystemId
    {
        get;
        private set => field = value?.Trim();
    }

    public bool CanSync => IsActive
        && IsValidConfiguration
        && HasActiveIntegrationObjects;

    public override bool HasActiveIntegrationObjects => IsValidConfiguration
        && (Configuration.WorkProcesses.Any(p => p.IntegrationIsActive)
        || Configuration.Workspaces.Any(p => p.IntegrationIsActive));

    public Result Update(string name, string? description, string organization, string personalAccessToken, bool configurationIsValid, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var newName = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
            var newDescription = description?.NullIfWhiteSpacePlusTrim();
            var newOrganization = Guard.Against.NullOrWhiteSpace(organization, nameof(organization)).Trim();
            var newPersonalAccessToken = Guard.Against.NullOrWhiteSpace(personalAccessToken, nameof(personalAccessToken)).Trim();

            if (!UpdateValuesChanged(newName, newDescription, newOrganization, newPersonalAccessToken, configurationIsValid))
                return Result.Success();

            Name = newName;
            Description = newDescription;
            IsValidConfiguration = configurationIsValid;

            Configuration.Organization = newOrganization;
            Configuration.PersonalAccessToken = newPersonalAccessToken;

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    /// <summary>
    /// Temporary method to set the system id.  The system id should not change after creation. 
    /// </summary>
    /// <remarks>This property was not originally a part of the model which is why we need a way to update it.</remarks>
    /// <param name="systemId"></param>
    /// <returns></returns>
    public Result SetSystemId(string systemId)
    {
        if (!string.IsNullOrWhiteSpace(SystemId))
        {
            return SystemId == systemId?.Trim()
                ? Result.Success()
                : Result.Failure("SystemId has already been set and cannot be changed to a different value.");
        }

        SystemId = systemId;

        return Result.Success();
    }

    public Result SyncWorkspaces(IEnumerable<AzureDevOpsBoardsWorkspace> workspaces, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));
            bool hasChanges = false;

            // remove workspaces that are not in the new list
            var workspacesToRemove = Configuration.Workspaces.Where(w => !workspaces.Any(nw => nw.ExternalId == w.ExternalId)).ToList();
            foreach (var workspace in workspacesToRemove)
            {
                // TODO - what if the workspace had been configured to sync and has data?
                var result = Configuration.RemoveWorkspace(workspace);
                if (result.IsFailure)
                    return result;

                // remove any teams associated with the workspace if any exist
                if (TeamConfiguration?.WorkspaceTeams.Any(t => t.WorkspaceId == workspace.ExternalId) == true)
                {
                    var removeTeamsResult = TeamConfiguration.RemoveTeamsForWorkspace(workspace.ExternalId);
                    if (removeTeamsResult.IsFailure)
                        return removeTeamsResult;
                }

                hasChanges = true;
            }

            // update existing or add new workspaces
            foreach (var workspace in workspaces)
            {
                var existing = Configuration.Workspaces.FirstOrDefault(w => w.ExternalId == workspace.ExternalId);
                if (existing is not null)
                {
                    // compare before calling Update
                    if (!string.Equals(existing.Name, workspace.Name, StringComparison.Ordinal)
                        || !string.Equals(existing.Description, workspace.Description, StringComparison.Ordinal)
                        || existing.WorkProcessId != workspace.WorkProcessId)
                    {
                        var result = existing.Update(workspace.Name, workspace.Description, workspace.WorkProcessId);
                        if (result.IsFailure)
                            return result;

                        hasChanges = true;
                    }
                }
                else
                {
                    var result = Configuration.AddWorkspace(AzureDevOpsBoardsWorkspace.Create(
                        workspace.ExternalId,
                        workspace.Name,
                        workspace.Description,
                        workspace.WorkProcessId));

                    if (result.IsFailure)
                        return result;

                    hasChanges = true;
                }
            }

            if (hasChanges)
                AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public Result SyncProcesses(IEnumerable<AzureDevOpsBoardsWorkProcess> processes, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));
            bool hasChanges = false;

            // remove processes that are not in the new list
            var processesToRemove = Configuration.WorkProcesses.Where(w => !processes.Any(nw => nw.ExternalId == w.ExternalId)).ToList();
            foreach (var process in processesToRemove)
            {
                // TODO - what if the process had been configured to sync and has data?
                var result = Configuration.RemoveWorkProcess(process);
                if (result.IsFailure)
                    return result;

                hasChanges = true;
            }

            // update existing or add new processes
            foreach (var process in processes)
            {
                var existingProcess = Configuration.WorkProcesses.FirstOrDefault(w => w.ExternalId == process.ExternalId);
                if (existingProcess is not null)
                {
                    // compare before calling Update
                    if (!string.Equals(existingProcess.Name, process.Name, StringComparison.Ordinal)
                        || !string.Equals(existingProcess.Description, process.Description, StringComparison.Ordinal))
                    {
                        var result = existingProcess.Update(
                            process.Name,
                            process.Description);

                        if (result.IsFailure)
                            return result;

                        hasChanges = true;
                    }
                }
                else
                {
                    var result = Configuration.AddWorkProcess(AzureDevOpsBoardsWorkProcess.Create(
                        process.ExternalId,
                        process.Name,
                        process.Description));

                    if (result.IsFailure)
                        return result;

                    hasChanges = true;
                }
            }

            // only raise domain event if something actually changed
            if (hasChanges)
                AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public Result SyncTeams(List<IExternalTeam> teams, Instant timestamp)
    {
        try
        {
            TeamConfiguration ??= AzureDevOpsBoardsTeamConfiguration.CreateEmpty();
            bool hasChanges = false;

            var teamsToRemove = TeamConfiguration.WorkspaceTeams
                .Where(t => !teams.Any(nw => nw.Id == t.TeamId))
                .Select(t => t.TeamId)
                .ToArray();

            if (teamsToRemove.Length > 0)
            {
                var removeResult = TeamConfiguration.RemoveTeams(teamsToRemove);
                if (removeResult.IsFailure)
                    return removeResult;

                hasChanges = true;
            }

            foreach (var team in teams)
            {
                var existing = TeamConfiguration.WorkspaceTeams.FirstOrDefault(t => t.TeamId == team.Id);
                if (existing is not null)
                {
                    // compare before calling Upsert to avoid no-op
                    if (existing.WorkspaceId != team.WorkspaceId
                        || !string.Equals(existing.TeamName, team.Name, StringComparison.Ordinal)
                        || existing.BoardId != team.BoardId)
                    {
                        var result = TeamConfiguration.UpsertWorkspaceTeam(team.WorkspaceId, team.Id, team.Name, team.BoardId);
                        if (result.IsFailure)
                            return result;

                        hasChanges = true;
                    }
                }
                else
                {
                    var result = TeamConfiguration.UpsertWorkspaceTeam(team.WorkspaceId, team.Id, team.Name, team.BoardId);
                    if (result.IsFailure)
                        return result;

                    hasChanges = true;
                }
            }

            if (hasChanges)
                AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public Result UpdateWorkProcessIntegrationState(IntegrationRegistration<Guid, Guid> registration, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var workProcess = Configuration.WorkProcesses.FirstOrDefault(wp => wp.ExternalId == registration.ExternalId);
            if (workProcess is null)
                return Result.Failure($"Unable to find work process with id {registration.ExternalId} in Azure DevOps connection with id {Id}.");

            // if already has integration, only update if the active flag changed
            if (workProcess.HasIntegration)
            {
                // If the live registration points at a different InternalId than what we hold,
                // the underlying Wayd.Work.WorkProcess was deleted and recreated. Rebind to
                // the live row rather than continuing to point at a dead id.
                if (workProcess.IntegrationState is not null
                    && workProcess.IntegrationState.InternalId != registration.IntegrationState.InternalId)
                {
                    var replaceResult = workProcess.ReplaceIntegrationState(registration.IntegrationState);
                    if (replaceResult.IsFailure)
                        return replaceResult;
                }
                else
                {
                    if (workProcess.IntegrationState is not null && workProcess.IntegrationState.IsActive == registration.IntegrationState.IsActive)
                    {
                        // no change
                        return Result.Success();
                    }

                    var setResult = workProcess.UpdateIntegrationState(registration.IntegrationState.IsActive);
                    if (setResult.IsFailure)
                        return setResult;
                }
            }
            else
            {
                var setResult = workProcess.AddIntegrationState(registration.IntegrationState);
                if (setResult.IsFailure)
                    return setResult;
            }

            // only raise domain event if something actually changed
            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public Result ClearWorkProcessIntegrationState(Guid workProcessExternalId, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var workProcess = Configuration.WorkProcesses.FirstOrDefault(wp => wp.ExternalId == workProcessExternalId);
            if (workProcess is null)
                return Result.Failure($"Unable to find work process with id {workProcessExternalId} in Azure DevOps connection with id {Id}.");

            if (!workProcess.HasIntegration)
                return Result.Success();

            var clearResult = workProcess.RemoveIntegrationState();
            if (clearResult.IsFailure)
                return clearResult;

            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public Result UpdateWorkspaceIntegrationState(IntegrationRegistration<Guid, Guid> registration, Instant timestamp)
    {
        try
        {
            Guard.Against.Null(Configuration, nameof(Configuration));

            var workspace = Configuration.Workspaces.FirstOrDefault(wp => wp.ExternalId == registration.ExternalId);
            if (workspace is null)
                return Result.Failure($"Unable to find workspace with id {registration.ExternalId} in Azure DevOps connection with id {Id}.");

            // if already has integration, only update if the active flag changed
            if (workspace.HasIntegration)
            {
                if (workspace.IntegrationState is not null && workspace.IntegrationState.IsActive == registration.IntegrationState.IsActive)
                {
                    // no change
                    return Result.Success();
                }

                var setResult = workspace.UpdateIntegrationState(registration.IntegrationState.IsActive);
                if (setResult.IsFailure)
                    return setResult;
            }
            else
            {
                var setResult = workspace.AddIntegrationState(registration.IntegrationState);
                if (setResult.IsFailure)
                    return setResult;
            }

            // only raise domain event if something actually changed
            AddDomainEvent(EntityUpdatedEvent.WithEntity(this, timestamp));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.ToString());
        }
    }

    public static AzureDevOpsBoardsConnection Create(string name, string? description, string? systemId, AzureDevOpsBoardsConnectionConfiguration configuration, bool configurationIsValid, AzureDevOpsBoardsTeamConfiguration? teamConfiguration, Instant timestamp)
    {
        var connector = new AzureDevOpsBoardsConnection(name, description, systemId, configuration, configurationIsValid, teamConfiguration);

        connector.AddDomainEvent(EntityCreatedEvent.WithEntity(connector, timestamp));

        return connector;
    }

    private bool UpdateValuesChanged(string name, string? description, string organization, string personalAccessToken, bool configurationIsValid)
    {
        // ordered by most likely to change
        if (!string.Equals(Name, name, StringComparison.Ordinal)) return true;
        if (!string.Equals(Description, description, StringComparison.Ordinal)) return true;
        if (!string.Equals(Configuration.PersonalAccessToken, personalAccessToken, StringComparison.Ordinal)) return true;
        if (IsValidConfiguration != configurationIsValid) return true;
        if (!string.Equals(Configuration.Organization, organization, StringComparison.Ordinal)) return true;
        return false;
    }
}
