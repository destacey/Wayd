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
}