using Mapster;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;

public sealed record AzureOpenAIConnectionConfigurationDto : IMapFrom<AzureOpenAIConnectionConfiguration>
{
    /// <summary>
    /// Azure OpenAI resource URL.
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the API key for Azure OpenAI resource.
    /// </summary>
    /// <remarks>Masked to a fixed-width placeholder when returned from the API.</remarks>
    public required string ApiKey { get; set; }

    /// <summary>
    /// The OpenAI model deployment name to use for this connection (e.g. "gpt-4o")
    /// </summary>
    public required string DeploymentName { get; set; }

    /// <summary>
    /// Default temperature for AI responses (0.0 = deterministic, higher = more random)
    /// </summary>
    public double DefaultTemperature { get; set; }

    /// <summary>
    /// Default maximum output tokens for AI responses
    /// </summary>
    public int DefaultMaxOutputTokens { get; set; }

    /// <summary>
    /// Indicates whether JSON mode is preferred for AI responses
    /// </summary>
    public bool JsonModePreferred { get; set; }

    /// <summary>Replaces the API key with the fixed-width placeholder. See <see cref="ConnectionSecret"/>.</summary>
    public void MaskApiKey()
        => ApiKey = ConnectionSecret.Masked(ApiKey);
}
