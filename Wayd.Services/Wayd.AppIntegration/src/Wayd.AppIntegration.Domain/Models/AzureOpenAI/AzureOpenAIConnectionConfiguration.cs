using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Wayd.Common.Domain.DataProtection;

namespace Wayd.AppIntegration.Domain.Models.AzureOpenAI;

public sealed class AzureOpenAIConnectionConfiguration
{
    [JsonConstructor]
    private AzureOpenAIConnectionConfiguration() { }

    [SetsRequiredMembers]
    public AzureOpenAIConnectionConfiguration(string apiKey, string deploymentName, string baseUrl, double defaultTemperature = 0.1, int defaultMaxOutputTokens = 400, bool jsonModePreferred = true)
    {
        ApiKey = apiKey.Trim();
        DeploymentName = deploymentName.Trim();
        BaseUrl = baseUrl.Trim();
        DefaultTemperature = defaultTemperature;
        DefaultMaxOutputTokens = defaultMaxOutputTokens;
        JsonModePreferred = jsonModePreferred;
        ConfigVersion = 1;
    }

    /// <summary>
    /// Gets or sets the API key for Azure OpenAI resource via ApiKeyCredential().
    /// </summary>
    [Encrypted]
    public required string ApiKey { get; set; }

    /// <summary>
    /// Azure OpenAI resource URL.
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// The OpenAI model name to use for this connection (e.g. "gpt-4o")
    /// </summary>
    public required string DeploymentName { get; set; }

    #region Common AI Settings

    public double DefaultTemperature { get; set; }

    public int DefaultMaxOutputTokens { get; set; }

    public bool JsonModePreferred { get; set; }

    #endregion

    public int ConfigVersion { get; init; }

    /// <summary>
    /// Whether this configuration names the same deployment as <paramref name="baseUrl"/> and
    /// <paramref name="deploymentName"/>, ignoring case, surrounding whitespace and a trailing slash on the URL.
    /// </summary>
    public bool IsSameDeployment(string baseUrl, string deploymentName) =>
        string.Equals(NormalizeBaseUrl(BaseUrl), NormalizeBaseUrl(baseUrl), StringComparison.OrdinalIgnoreCase)
        && string.Equals(DeploymentName.Trim(), deploymentName.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.Trim().TrimEnd('/');

    /// <summary>
    /// Whether <paramref name="baseUrl"/> can be the resource URL: absolute HTTPS with no credentials in it,
    /// since the API key is sent to it.
    /// </summary>
    public static bool IsValidBaseUrl(string? baseUrl) =>
        Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo);
}