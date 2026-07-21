using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Logging;
using Wayd.Common.Extensions;
using Wayd.Integrations.AzureDevOps.Clients;
using Wayd.Integrations.AzureDevOps.Extensions;
using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Models.Projects;

namespace Wayd.Integrations.AzureDevOps.Services;

internal sealed class ProjectService(HttpClient httpClient, string organizationUrl, string token, string apiVersion, ILogger<ProjectService> logger)
{
    private readonly ProjectClient _projectClient = new(httpClient, organizationUrl, token, apiVersion);
    private readonly ILogger<ProjectService> _logger = logger;
    private readonly int _maxBatchSize = 100;

    // Deliberately small: enough to collapse the per-team settings N+1 without bursting against
    // Azure DevOps throttling or competing with interactive traffic on the API host.
    private readonly int _teamSettingsMaxConcurrency = 4;

    /// <summary>
    /// Retrieves a list of projects from Azure DevOps in batches.
    /// </summary>
    /// <remarks>This method fetches projects in batches to handle large datasets efficiently. It continues
    /// retrieving batches until all projects are fetched or an error occurs. If an error occurs during the retrieval
    /// process, the method logs the error and returns a failure result.</remarks>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The operation will terminate early if the token is canceled.</param>
    /// <returns>A <see cref="Result{T}"/> containing a list of <see cref="ProjectDto"/> objects if the operation is successful;
    /// otherwise, a failure result with an error message.</returns>
    public async Task<Result<List<ProjectDto>>> GetProjects(CancellationToken cancellationToken)
    {
        try
        {
            List<ProjectDto> projects = [];

            while (true)
            {
                var batch = await _projectClient.GetProjects(top: _maxBatchSize, skip: projects.Count, cancellationToken).ConfigureAwait(false);
                if (!batch.IsSuccessful)
                {
                    _logger.LogError("Error getting projects from Azure DevOps: {ErrorMessage}.", batch.GetErrorText());
                    return Result.Failure<List<ProjectDto>>(batch.GetErrorText());
                }

                if (batch.Data is null)
                    break;

                projects.AddRange(batch.Data.Items);

                if (batch.Data.Count < _maxBatchSize)
                    break;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{ProjectCount} projects found ", projects.Count);

            return projects;
        }
        catch (OperationCanceledException)
        {
            // A genuine cancellation (caller's token fired) is not a sync failure — let it
            // propagate so the caller's cancellation handling (e.g. marking a sync run
            // cancelled rather than partially failed) actually runs.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting projects from Azure DevOps");
            return Result.Failure<List<ProjectDto>>(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the details of a project, including its properties, from Azure DevOps.
    /// </summary>
    /// <remarks>This method fetches the project details and its associated properties from Azure DevOps. If
    /// the project or its properties cannot be retrieved due to an error or if they do not exist, the method logs the
    /// error and returns a failure result.</remarks>
    /// <param name="projectIdOrName">The unique identifier or name of the project to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Result{T}"/> containing the project details as a <see cref="ProjectDetailsDto"/> if successful;
    /// otherwise, a failure result with an error message.</returns>
    public async Task<Result<ProjectDetailsDto>> GetProject(string projectIdOrName, CancellationToken cancellationToken)
    {
        try
        {
            var projectResponse = await _projectClient.GetProject(projectIdOrName, cancellationToken).ConfigureAwait(false);
            if (!projectResponse.IsSuccessful && projectResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var statusDescription = projectResponse.StatusCode is 0 ? "Connection Error" : projectResponse.StatusDescription;
                var errorMessage = projectResponse.ErrorMessage is null ? statusDescription : $"{statusDescription} - {projectResponse.ErrorMessage}";
                _logger.LogError("Error getting project {ProjectIdOrName} from Azure DevOps: {ErrorMessage}.", projectIdOrName, errorMessage);
                return Result.Failure<ProjectDetailsDto>(errorMessage);
            }
            else if ((!projectResponse.IsSuccessful && projectResponse.StatusCode is HttpStatusCode.NotFound) || projectResponse.Data is null)
            {
                var errorMesssage = projectResponse.IsSuccessful ? "No project data returned" : projectResponse.StatusDescription;
                _logger.LogError("Error getting project {ProjectIdOrName} from Azure DevOps: {ErrorMessage}.", projectIdOrName, errorMesssage);
                return Result.Failure<ProjectDetailsDto>(errorMesssage);
            }

            var propertiesResponse = await _projectClient.GetProjectProperties(projectResponse.Data.Id, cancellationToken).ConfigureAwait(false);
            if (!propertiesResponse.IsSuccessful && propertiesResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var statusDescription = propertiesResponse.StatusCode is 0 ? "Connection Error" : propertiesResponse.StatusDescription;
                var errorMessage = propertiesResponse.ErrorMessage is null ? statusDescription : $"{statusDescription} - {propertiesResponse.ErrorMessage}";
                _logger.LogError("Error getting project properties {ProjectId} from Azure DevOps: {ErrorMessage}.", projectIdOrName, errorMessage);
                return Result.Failure<ProjectDetailsDto>(errorMessage);
            }
            else if ((!propertiesResponse.IsSuccessful && propertiesResponse.StatusCode is HttpStatusCode.NotFound) || propertiesResponse.Data is null)
            {
                var errorMesssage = propertiesResponse.IsSuccessful ? "No project properties data returned" : propertiesResponse.StatusDescription;
                _logger.LogError("Error getting project properties {ProjectId} from Azure DevOps: {ErrorMessage}.", projectIdOrName, errorMesssage);
                return Result.Failure<ProjectDetailsDto>(errorMesssage);
            }

            return ProjectDetailsDto.Create(projectResponse.Data, [.. propertiesResponse.Data.Value]);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting project {ProjectId} from Azure DevOps", projectIdOrName);
            return Result.Failure<ProjectDetailsDto>(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the teams for the specified project IDs from Azure DevOps.
    /// </summary>
    /// <param name="projectIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Result<List<IExternalTeam>>> GetTeams(Guid[] projectIds, CancellationToken cancellationToken)
    {
        List<IExternalTeam> teams = [];

        if (projectIds is null || projectIds.Length == 0)
        {
            string message = "No project ids provided to get teams from Azure DevOps.";
            _logger.LogWarning(message);
            return Result.Failure<List<IExternalTeam>>(message);
        }

        Guid currentProjectId = Guid.Empty;

        try
        {
            foreach (var id in projectIds)
            {
                currentProjectId = id;

                // The teams endpoint caps results (default 100), so page like GetProjects does.
                List<TeamDto> projectTeams = [];
                while (true)
                {
                    var response = await _projectClient.GetProjectTeams(id, top: _maxBatchSize, skip: projectTeams.Count, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessful)
                    {
                        _logger.LogError("Error getting teams for project {ProjectId} from Azure DevOps: {ErrorMessage}.", id, response.GetErrorText());
                        return Result.Failure<List<IExternalTeam>>($"Error getting teams for project {id} from Azure DevOps"); // each project should have at least one team
                    }

                    if (response.Data is null)
                        break;

                    projectTeams.AddRange(response.Data.Value);

                    if (response.Data.Value.Count < _maxBatchSize)
                        break;
                }

                if (projectTeams.Count == 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("No teams found for project {ProjectId}.", id);
                    return Result.Failure<List<IExternalTeam>>($"Error getting teams for project {id} from Azure DevOps"); // each project should have at least one team
                }

                // Fetch team settings (backlog iteration → boardId) with bounded concurrency. The calls
                // are independent I/O-bound lookups, so the parallelism costs no thread-pool capacity
                // while awaiting; the small degree keeps the burst polite to both Azure DevOps
                // throttling and the API host serving interactive traffic. Results land in a slot per
                // team to keep the output order deterministic; a failed settings lookup skips just
                // that team, matching the previous sequential behavior.
                var teamSlots = new IExternalTeam?[projectTeams.Count];
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = _teamSettingsMaxConcurrency,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(Enumerable.Range(0, projectTeams.Count), parallelOptions, async (index, ct) =>
                {
                    var team = projectTeams[index];
                    var teamSettingsResponse = await _projectClient.GetProjectTeamsSettings(id, team.Id, ct).ConfigureAwait(false);
                    if (!teamSettingsResponse.IsSuccessful)
                    {
                        _logger.LogError("Error getting team settings for team {TeamId} in project {ProjectId} from Azure DevOps: {ErrorMessage}.", team.Id, id, teamSettingsResponse.GetErrorText());
                        return;
                    }
                    if (teamSettingsResponse.Data is null)
                    {
                        _logger.LogWarning("No team settings found for team {TeamId} in project {ProjectId}.", team.Id, id);
                        return;
                    }

                    teamSlots[index] = team.ToAzdoTeam(id, teamSettingsResponse.Data.BacklogIteration?.Id);
                }).ConfigureAwait(false);

                foreach (var team in teamSlots)
                {
                    if (team is not null)
                        teams.Add(team);
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("{TeamCount} teams found for project {ProjectId}.", projectTeams.Count, id);
            }

            return teams;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting teams for project {ProjectId} from Azure DevOps", currentProjectId);
            return Result.Failure<List<IExternalTeam>>(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves the area paths for a specified project from Azure DevOps.
    /// </summary>
    /// <remarks>This method retrieves the hierarchical area paths for the specified project by querying Azure
    /// DevOps. If the operation is unsuccessful, the result will contain an error message. If no area paths are found,
    /// the result will indicate failure with an appropriate message.</remarks>
    /// <param name="projectName">The name of the project for which to retrieve area paths. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a <see
    /// cref="Result{T}"/> object that holds a list of <see cref="ClassificationNodeResponse"/> representing the area
    /// paths if successful, or an error message if the operation fails.</returns>
    public async Task<Result<List<ClassificationNodeResponse>>> GetAreaPaths(string projectName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _projectClient.GetAreaPaths(projectName, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                _logger.LogError("Error getting areas for project {ProjectId} from Azure DevOps: {ErrorMessage}.", projectName, response.GetErrorText());
                return Result.Failure<List<ClassificationNodeResponse>>(response.GetErrorText());
            }
            if (response.Data is null)
            {
                _logger.LogWarning("No areas found for project {ProjectId}.", projectName);
                return Result.Failure<List<ClassificationNodeResponse>>($"No areas found for project {projectName}");
            }

            var areaPaths = response.Data.FlattenHierarchy(a => a.Children).ToList();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{AreaCount} areas found for project {ProjectId}.", areaPaths.Count, projectName);

            return areaPaths;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting areas for project {ProjectId} from Azure DevOps", projectName);
            return Result.Failure<List<ClassificationNodeResponse>>(ex.Message);
        }
    }

    /// <summary>
    /// Retrieves a list of iterations for the specified project, optionally mapping iterations to team settings.
    /// </summary>
    /// <remarks>This method retrieves iteration paths from Azure DevOps for the specified project. If team
    /// settings are provided, the method maps iterations to the corresponding teams. The method logs errors and
    /// warnings for unsuccessful operations or when no iterations are found.</remarks>
    /// <param name="projectName">The name of the project for which to retrieve iterations. Cannot be null or empty.</param>
    /// <param name="teamSettings">An optional dictionary mapping team IDs to their corresponding iteration IDs. If provided, the method will
    /// associate iterations with the specified teams.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains a <see
    /// cref="Result{T}"/> object that holds a list of <see cref="IterationDto"/> instances if successful, or an error
    /// message if the operation fails.</returns>
    public async Task<Result<List<IterationDto>>> GetIterations(string projectName, Dictionary<Guid, Guid?>? teamSettings, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _projectClient.GetIterationPaths(projectName, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                _logger.LogError("Error getting iterations for project {ProjectId} from Azure DevOps: {ErrorMessage}.", projectName, response.GetErrorText());
                return Result.Failure<List<IterationDto>>(response.GetErrorText());
            }
            if (response.Data is null)
            {
                _logger.LogWarning("No iterations found for project {ProjectId}.", projectName);
                return Result.Failure<List<IterationDto>>($"No iterations found for project {projectName}");
            }

            Dictionary<Guid, Guid> iterationTeamMapping = ConvertTeamSettingsToIterationTeamMapping(teamSettings);

            var iterations = FlattenAndSetTeamIds(response.Data, iterationTeamMapping).ToList();

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{IterationCount} iterations found for project {ProjectId}.", iterations.Count, projectName);

            return iterations;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting iterations for project {ProjectId} from Azure DevOps", projectName);
            return Result.Failure<List<IterationDto>>(ex.Message);
        }
    }

    /// <summary>
    /// Performs a single-pass traversal of the iteration hierarchy to flatten it and assign team IDs.
    /// This optimized approach eliminates the need for intermediate DTO conversions and multiple tree traversals.
    /// </summary>
    /// <param name="root">The root iteration node from Azure DevOps</param>
    /// <param name="iterationTeamMapping">Mapping of iteration IDs to team IDs</param>
    /// <returns>Flattened list of iterations with team IDs assigned</returns>
    private static IEnumerable<IterationDto> FlattenAndSetTeamIds(
        IterationNodeResponse root,
        Dictionary<Guid, Guid> iterationTeamMapping)
    {
        Stack<(IterationNodeResponse Node, Guid? ParentTeamId)> stack = new();
        stack.Push((root, null));

        while (stack.Count > 0)
        {
            var (current, parentTeamId) = stack.Pop();

            // Determine team ID: use mapped value if exists, otherwise inherit from parent
            var teamId = iterationTeamMapping.TryGetValue(current.Identifier, out var mappedTeamId)
                ? mappedTeamId
                : parentTeamId;

            // Yield flattened result immediately - no intermediate allocations
            yield return IterationDto.FromIterationNodeResponse(current, teamId);

            // Push children onto stack with inherited team ID
            if (current.Children is not null)
            {
                foreach (var child in current.Children)
                {
                    stack.Push((child, teamId));
                }
            }
        }
    }

    private Dictionary<Guid, Guid> ConvertTeamSettingsToIterationTeamMapping(Dictionary<Guid, Guid?>? teamSettings)
    {
        if (teamSettings is null || teamSettings.Count == 0)
            return [];

        var iterationTeamMapping = new Dictionary<Guid, Guid>(teamSettings.Count);

        foreach (var kv in teamSettings)
        {
            var teamId = kv.Key;
            var iterationId = kv.Value;

            if (iterationId is null)
                continue;

            if (!iterationTeamMapping.TryAdd(iterationId.Value, teamId))
            {
                _iterationAlreadyMapped(_logger, iterationId.Value, teamId, null);
            }
        }

        return iterationTeamMapping;
    }

    // Cached logger delegate to avoid per-call allocations/boxing
    private static readonly Action<ILogger, Guid, Guid, Exception?> _iterationAlreadyMapped =
        LoggerMessage.Define<Guid, Guid>(LogLevel.Warning, AppEventId.Integrations_AzureDevOps_ProjectService_DuplicateIterationTeamMapping.ToEventId(),
            "Iteration {IterationId} is already mapped to team {TeamId}.");
}
