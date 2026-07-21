using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Integrations.AzureDevOps.Models.Projects;
using Wayd.Integrations.AzureDevOps.Models.WorkItems;
using Wayd.Integrations.AzureDevOps.Services;
using Wayd.Integrations.AzureDevOps.Utils;

namespace Wayd.Integrations.AzureDevOps;

public class AzureDevOpsService(
    ILogger<AzureDevOpsService> logger,
    ILoggerFactory loggerFactory,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    IMemoryCache memoryCache) : IAzureDevOpsService
{
    // https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rest-api-versioning?view=azure-devops#supported-versions
    // 7.1 requires Azure DevOps Services (or Server 2025+). Wayd only connects to the hosted
    // service — if on-prem Server support ever enters scope, 7.0 is the floor Server 2022 speaks.
    private readonly string _apiVersion = "7.1";

    private readonly ILogger<AzureDevOpsService> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IMemoryCache _memoryCache = memoryCache;

    public async Task<Result> TestConnection(AzureDevOpsConnectionContext connection)
    {
        try
        {
            // use the GetInstanceId method to test the connection
            var result = await GetSystemId(connection, CancellationToken.None).ConfigureAwait(false);

            return result.IsSuccess
                ? Result.Success()
                : Result.Failure("Unable to verify connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown testing Azure DevOps connection.");
            return Result.Failure(ex.InnerException?.Message ?? ex.Message);
        }
    }

    public async Task<Result<string>> GetSystemId(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken)
    {
        var generalService = CreateGeneralService(connection);

        var result = await generalService.GetConnectionData(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<string>(result.Error);

        return result.Value is not null
            ? result.Value.InstanceId
            : Result.Failure<string>("No systemId returned.");
    }

    public async Task<Result<List<IExternalWorkProcess>>> GetWorkProcesses(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken)
    {
        var processService = CreateProcessService(connection);

        var result = await processService.GetProcesses(cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value.ToList<IExternalWorkProcess>()
            : Result.Failure<List<IExternalWorkProcess>>(result.Error);
    }

    public async Task<Result<IExternalWorkProcessConfiguration>> GetWorkProcess(AzureDevOpsConnectionContext connection, Guid processId, CancellationToken cancellationToken)
    {
        var processService = CreateProcessService(connection);

        var result = await processService.GetProcess(processId, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value
            : Result.Failure<IExternalWorkProcessConfiguration>(result.Error);
    }

    public async Task<Result<IExternalWorkspaceConfiguration>> GetWorkspace(AzureDevOpsConnectionContext connection, Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await GetProject(connection, workspaceId.ToString(), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
            return Result.Failure<IExternalWorkspaceConfiguration>(result.Error);

        if (!result.Value.HasProcessTemplateType)
            return Result.Failure<IExternalWorkspaceConfiguration>("Workspace does not have a process template type.");

        return result.Value.ToAzdoWorkspaceConfiguration();
    }

    public async Task<Result<List<IExternalWorkspace>>> GetWorkspaces(AzureDevOpsConnectionContext connection, CancellationToken cancellationToken)
    {
        var projectService = CreateProjectService(connection);

        var result = await projectService.GetProjects(cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value.ToAzdoWorkspaces().ToList<IExternalWorkspace>()
            : Result.Failure<List<IExternalWorkspace>>(result.Error);
    }

    public async Task<Result<List<IExternalTeam>>> GetTeams(AzureDevOpsConnectionContext connection, Guid[] projectIds, CancellationToken cancellationToken)
    {
        var projectService = CreateProjectService(connection);

        var result = await projectService.GetTeams(projectIds, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<List<IExternalTeam>>(result.Error);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("{TeamCount} teams found for organization {organizationUrl}.", result.Value.Count, connection.OrganizationUrl);

        return result.Value;
    }

    public async Task<Result<List<IExternalIteration<AzdoIterationMetadata>>>> GetIterations(AzureDevOpsConnectionContext connection, string projectName, Dictionary<Guid, Guid?> teamSettings, CancellationToken cancellationToken)
    {
        var projectResult = await GetProject(connection, projectName, cancellationToken).ConfigureAwait(false);
        if (projectResult.IsFailure)
            return Result.Failure<List<IExternalIteration<AzdoIterationMetadata>>>($"Unable to get details for project {projectName}");

        var iterationsResult = await GetOrFetchIterationsAsync(connection, projectName, teamSettings, cancellationToken).ConfigureAwait(false);

        return iterationsResult.IsSuccess
            ? iterationsResult.Value.ToIExternalIterations(_dateTimeProvider.Now, projectResult.Value.Id)
            : Result.Failure<List<IExternalIteration<AzdoIterationMetadata>>>(iterationsResult.Error);
    }

    public async Task<Result<List<IExternalWorkItem>>> GetWorkItems(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, Dictionary<Guid, Guid?> teamSettings, CancellationToken cancellationToken)
    {
        var iterationsResult = await GetOrFetchIterationsAsync(connection, projectName, teamSettings, cancellationToken).ConfigureAwait(false);
        if (iterationsResult.IsFailure)
            return Result.Failure<List<IExternalWorkItem>>(iterationsResult.Error);

        var cachedIterations = iterationsResult.Value;

        var workItemService = CreateWorkItemService(connection);

        var result = await workItemService.GetWorkItems(projectName, lastChangedDate, workItemTypes, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value.ToIExternalWorkItems(cachedIterations, _logger)
            : Result.Failure<List<IExternalWorkItem>>(result.Error);
    }

    public async Task<Result<List<IExternalWorkItemLink>>> GetParentLinkChanges(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        var workItemService = CreateWorkItemService(connection);

        var result = await workItemService.GetParentLinkChanges(projectName, lastChangedDate, workItemTypes, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value.ToIExternalWorkItemLinks()
            : Result.Failure<List<IExternalWorkItemLink>>(result.Error);
    }

    public async Task<Result<List<IExternalWorkItemLink>>> GetDependencyLinkChanges(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        var workItemService = CreateWorkItemService(connection);

        var result = await workItemService.GetDependencyLinkChanges(projectName, lastChangedDate, workItemTypes, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value.ToIExternalWorkItemLinks()
            : Result.Failure<List<IExternalWorkItemLink>>(result.Error);
    }

    public async Task<Result<int[]>> GetDeletedWorkItemIds(AzureDevOpsConnectionContext connection, string projectName, DateTime lastChangedDate, string[] workItemTypes, CancellationToken cancellationToken)
    {
        var workItemService = CreateWorkItemService(connection);

        var result = await workItemService.GetDeletedWorkItemIds(projectName, lastChangedDate, workItemTypes, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? result.Value
            : Result.Failure<int[]>(result.Error);
    }

    private async Task<Result<ProjectDetailsDto>> GetProject(AzureDevOpsConnectionContext connection, string projectIdOrName, CancellationToken cancellationToken)
    {
        var projectService = CreateProjectService(connection);

        return await projectService.GetProject(projectIdOrName, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<List<IterationDto>>> GetOrFetchIterationsAsync(AzureDevOpsConnectionContext connection, string projectName, Dictionary<Guid, Guid?>? teamSettings, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        var cacheKey = CacheKeyGenerator.GetCacheKey("azdo-iterations", connection.OrganizationUrl, projectName, teamSettings);

        if (!forceRefresh && _memoryCache.TryGetValue(cacheKey, out List<IterationDto>? cached) && cached is not null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Returning cached iterations for {cacheKey}", cacheKey);

            return Result.Success(cached);
        }

        var projectService = CreateProjectService(connection);
        var result = await projectService.GetIterations(projectName, teamSettings, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Result.Failure<List<IterationDto>>(result.Error);

        // ensure we never cache null; allow empty list
        var toCache = result.Value ?? [];
        var iterationCacheOptions = new MemoryCacheEntryOptions
        {
            // the goal is to have a short-lived cache for a single sync run
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
        };

        _memoryCache.Set(cacheKey, toCache, iterationCacheOptions);

        return Result.Success(toCache);
    }

    // Each service gets a fresh HttpClient from the factory: instances are cheap wrappers over the
    // factory's pooled handler chain, which also carries the host's resilience pipeline.
    private HttpClient CreateHttpClient() => _httpClientFactory.CreateClient(AzureDevOpsHttpClient.Name);

    private GeneralService CreateGeneralService(AzureDevOpsConnectionContext connection) =>
        new(CreateHttpClient(), connection.OrganizationUrl, connection.PersonalAccessToken, _apiVersion, _loggerFactory.CreateLogger<GeneralService>());

    private ProcessService CreateProcessService(AzureDevOpsConnectionContext connection) =>
        new(CreateHttpClient(), connection.OrganizationUrl, connection.PersonalAccessToken, _apiVersion, _loggerFactory.CreateLogger<ProcessService>());

    private ProjectService CreateProjectService(AzureDevOpsConnectionContext connection) =>
        new(CreateHttpClient(), connection.OrganizationUrl, connection.PersonalAccessToken, _apiVersion, _loggerFactory.CreateLogger<ProjectService>());

    private WorkItemService CreateWorkItemService(AzureDevOpsConnectionContext connection) =>
        new(CreateHttpClient(), connection.OrganizationUrl, connection.PersonalAccessToken, _apiVersion, _loggerFactory.CreateLogger<WorkItemService>());
}
