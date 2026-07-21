using Wayd.Integrations.AzureDevOps.Models;
using Wayd.Integrations.AzureDevOps.Models.Contracts;
using Wayd.Integrations.AzureDevOps.Models.Projects;
using RestSharp;

namespace Wayd.Integrations.AzureDevOps.Clients;

internal sealed class ProjectClient : BaseClient
{
    internal ProjectClient(HttpClient httpClient, string organizationUrl, string token, string apiVersion)
        : base(httpClient, organizationUrl, token, apiVersion)
    { }

    internal async Task<RestResponse<AzdoListResponse<ProjectDto>>> GetProjects(int top, int skip, CancellationToken cancellationToken)
    {
        var request = new RestRequest("/_apis/projects", Method.Get);
        SetupRequest(request);
        request.AddParameter("$top", top);
        request.AddParameter("$skip", skip);

        return await ExecuteAsync<AzdoListResponse<ProjectDto>>(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<RestResponse<ProjectDto>> GetProject(string projectIdOrName, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/_apis/projects/{projectIdOrName}", Method.Get);
        SetupRequest(request);

        return await ExecuteAsync<ProjectDto>(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<RestResponse<ListResponse<PropertyDto>>> GetProjectProperties(Guid projectId, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/_apis/projects/{projectId}/properties", Method.Get);
        SetupRequest(request, true);
        request.AddParameter("keys", "System.ProcessTemplateType");

        return await ExecuteAsync<ListResponse<PropertyDto>>(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<RestResponse<ListResponse<TeamDto>>> GetProjectTeams(Guid projectId, int top, int skip, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/_apis/projects/{projectId}/teams", Method.Get);
        SetupRequest(request);
        request.AddParameter("$top", top);
        request.AddParameter("$skip", skip);

        return await ExecuteAsync<ListResponse<TeamDto>>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the team settings for the specified team.
    /// </summary>
    /// <param name="projectId"></param>
    /// <param name="teamId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<RestResponse<TeamSettingsResponse>> GetProjectTeamsSettings(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/{projectId}/{teamId}/_apis/work/teamsettings", Method.Get);
        SetupRequest(request);

        return await ExecuteAsync<TeamSettingsResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the root area path and all child area paths for the project.
    /// </summary>
    /// <param name="projectName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<RestResponse<ClassificationNodeResponse>> GetAreaPaths(string projectName, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/{projectName}/_apis/wit/classificationnodes/areas", Method.Get);
        SetupRequest(request);
        request.AddParameter("$depth", 100); // TODO: make this configurable

        return await ExecuteAsync<ClassificationNodeResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the root iteration path and all child iteration paths for the project.
    /// </summary>
    /// <param name="projectName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    internal async Task<RestResponse<IterationNodeResponse>> GetIterationPaths(string projectName, CancellationToken cancellationToken)
    {
        var request = new RestRequest($"/{projectName}/_apis/wit/classificationnodes/iterations", Method.Get);
        SetupRequest(request);
        request.AddParameter("$depth", 100); // TODO: make this configurable

        return await ExecuteAsync<IterationNodeResponse>(request, cancellationToken).ConfigureAwait(false);
    }
}
