using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Wayd.Integrations.AzureDevOps.Clients;
using Wayd.Integrations.AzureDevOps.Extensions;
using Wayd.Integrations.AzureDevOps.Models;

namespace Wayd.Integrations.AzureDevOps.Services;

internal sealed class GeneralService(HttpClient httpClient, string organizationUrl, string token, string apiVersion, ILogger<GeneralService> logger)
{
    private readonly GeneralClient _generalClient = new(httpClient, organizationUrl, token, apiVersion);
    private readonly ILogger<GeneralService> _logger = logger;

    public async Task<Result<ConnectionDataResponse?>> GetConnectionData(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _generalClient.GetConnectionData(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessful)
            {
                _logger.LogError("Error getting connection data from Azure DevOps: {ErrorMessage}.", response.GetErrorText());
                return Result.Failure<ConnectionDataResponse?>(response.GetErrorText());
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NonAuthoritativeInformation)
            {
                _logger.LogError("The request was not authorized with Azure DevOps.");
                return Result.Failure<ConnectionDataResponse?>("The request was not authorized with Azure DevOps.");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Azure DevOps Instance ID: {InstanceId}", response.Data?.InstanceId);

            return response.Data;
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
            _logger.LogError(ex, "Exception thrown getting connection data from Azure DevOps.");
            return Result.Failure<ConnectionDataResponse?>(ex.Message);
        }
    }
}
