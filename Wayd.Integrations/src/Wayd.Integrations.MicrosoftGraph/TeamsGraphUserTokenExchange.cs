using CSharpFunctionalExtensions;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces.Communication;

namespace Wayd.Integrations.MicrosoftGraph;

/// <summary>
/// On-behalf-of token exchange against the Teams communication app registration.
/// </summary>
public sealed class TeamsGraphUserTokenExchange(
    IHttpClientFactory httpClientFactory,
    ILogger<TeamsGraphUserTokenExchange> logger) : ITeamsGraphUserTokenExchange
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<TeamsGraphUserTokenExchange> _logger = logger;

    /// <inheritdoc />
    public async Task<Result<string>> ExchangeForGraphAccessTokenAsync(
        TeamsGraphCredentials connectionCredentials,
        string userAssertion,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken)
    {
        if (scopes.Count == 0)
            return Result.Failure<string>("At least one Microsoft Graph scope is required.");

        var tokenEndpoint =
            $"https://login.microsoftonline.com/{connectionCredentials.TenantId}/oauth2/v2.0/token";

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["client_id"] = connectionCredentials.ClientId,
            ["client_secret"] = connectionCredentials.ClientSecret,
            ["assertion"] = userAssertion,
            ["scope"] = string.Join(' ', scopes),
            ["requested_token_use"] = "on_behalf_of",
        };

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(TeamsGraphUserTokenExchange));
            using var response = await client.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(form),
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload?.AccessToken))
            {
                var error = payload?.ErrorDescription ?? payload?.Error ?? response.ReasonPhrase ?? "Unknown error";
                _logger.LogWarning(
                    "Teams Graph OBO failed for tenant {TenantId}: {Error}",
                    connectionCredentials.TenantId,
                    error);
                return Result.Failure<string>(
                    "Wayd could not obtain a delegated Microsoft Graph token for your signed-in user. "
                    + "The access token must be issued for this Teams app registration "
                    + $"(audience {connectionCredentials.ClientId}, scope api://{connectionCredentials.ClientId}/access_as_user), "
                    + "not for Wayd's sign-in API. "
                    + $"Entra reported: {error}");
            }

            return Result.Success(payload.AccessToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Teams Graph OBO request failed for tenant {TenantId}", connectionCredentials.TenantId);
            return Result.Failure<string>(
                $"Wayd could not exchange your Entra access token for Microsoft Graph: {ex.Message}");
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
