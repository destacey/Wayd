using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Integrations.AzureDevOps.Clients;
using Wayd.Integrations.AzureDevOps.Extensions;
using Wayd.Integrations.AzureDevOps.Models.Contracts;
using Wayd.Integrations.AzureDevOps.Models.Processes;

namespace Wayd.Integrations.AzureDevOps.Services;

internal sealed class ProcessService(HttpClient httpClient, string organizationUrl, string token, string apiVersion, ILogger<ProcessService> logger)
{
    private readonly ProcessClient _processClient = new(httpClient, organizationUrl, token, apiVersion);
    private readonly ILogger<ProcessService> _logger = logger;

    public async Task<Result<List<AzdoWorkProcess>>> GetProcesses(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _processClient.GetProcesses(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                _logger.LogError("Error getting processes from Azure DevOps: {ErrorMessage}.", response.GetErrorText());
                return Result.Failure<List<AzdoWorkProcess>>(response.GetErrorText());
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            {
                _logger.LogError("The request was not authorized with Azure DevOps.");
                return Result.Failure<List<AzdoWorkProcess>>("The request was not authorized with Azure DevOps.");
            }

            var processes = response.Data?.Items.ToAzdoWorkProcesses() ?? [];

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("{ProcessCount} processes found.", processes.Count);

            return Result.Success(processes);
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
            _logger.LogError(ex, "Exception thrown getting processes from Azure DevOps.");
            return Result.Failure<List<AzdoWorkProcess>>(ex.Message);
        }
    }

    public async Task<Result<AzdoWorkProcessConfiguration>> GetProcess(Guid processId, CancellationToken cancellationToken)
    {
        try
        {
            var processResult = await GetProcessById(processId, cancellationToken).ConfigureAwait(false);
            if (processResult.IsFailure)
                return Result.Failure<AzdoWorkProcessConfiguration>(processResult.Error);

            var behaviorsResult = await GetProcessBehaviors(processId, cancellationToken).ConfigureAwait(false);
            if (behaviorsResult.IsFailure)
                return Result.Failure<AzdoWorkProcessConfiguration>(behaviorsResult.Error);

            var workTypesResult = await GetProcessWorkItemTypes(processId, cancellationToken).ConfigureAwait(false);
            if (workTypesResult.IsFailure)
                return Result.Failure<AzdoWorkProcessConfiguration>(workTypesResult.Error);

            return Result.Success(processResult.Value.ToAzdoWorkProcessDetails(behaviorsResult.Value, workTypesResult.Value));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception thrown getting process {ProcessId} from Azure DevOps.", processId);
            return Result.Failure<AzdoWorkProcessConfiguration>(ex.Message);
        }
    }

    private async Task<Result<ProcessDto>> GetProcessById(Guid processId, CancellationToken cancellationToken)
    {
        var response = await _processClient.GetProcess(processId, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessful && response.StatusCode != HttpStatusCode.NotFound)
        {
            var statusDescription = response.StatusCode is 0 ? "Connection Error" : response.StatusDescription;
            var errorMessage = response.ErrorMessage is null ? statusDescription : $"{statusDescription} - {response.ErrorMessage}";
            _logger.LogError("Error getting process {ProcessId} from Azure DevOps: {ErrorMessage}.", processId, errorMessage);
            return Result.Failure<ProcessDto>(errorMessage);
        }
        else if ((!response.IsSuccessful && response.StatusCode is HttpStatusCode.NotFound) || response.Data is null)
        {
            var errorMesssage = response.IsSuccessful ? "No process data returned" : response.StatusDescription;
            _logger.LogError("Error getting process {ProcessId} from Azure DevOps: {ErrorMessage}.", processId, errorMesssage);
            return Result.Failure<ProcessDto>(errorMesssage);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Process {ProcessId} found.", processId);

        return Result.Success(response.Data);
    }

    private async Task<Result<List<BehaviorDto>>> GetProcessBehaviors(Guid processId, CancellationToken cancellationToken)
    {
        var response = await _processClient.GetBehaviors(processId, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Error getting behaviors for process {ProcessId} from Azure DevOps: {ErrorMessage}.", processId, response.GetErrorText());
            return Result.Failure<List<BehaviorDto>>(response.GetErrorText());
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("{BehaviorCount} behaviors found for process {ProcessId}.", response.Data?.Count ?? 0, processId);

        return Result.Success(response.Data?.Items ?? []);
    }

    private async Task<Result<List<ProcessWorkItemTypeDto>>> GetProcessWorkItemTypes(Guid processId, CancellationToken cancellationToken)
    {
        var response = await _processClient.GetWorkItemTypes(processId, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessful)
        {
            _logger.LogError("Error getting work item types for process {ProcessId} from Azure DevOps: {ErrorMessage}.", processId, response.GetErrorText());
            return Result.Failure<List<ProcessWorkItemTypeDto>>(response.GetErrorText());
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("{WorkItemTypeCount} work item types found for process {ProcessId}.", response.Data?.Count ?? 0, processId);

        return Result.Success(response.Data?.Items ?? []);
    }
}
