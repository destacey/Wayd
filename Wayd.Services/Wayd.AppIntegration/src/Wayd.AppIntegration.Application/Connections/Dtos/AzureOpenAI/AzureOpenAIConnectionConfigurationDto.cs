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
    /// <remarks>This will be masked when returned from the API.</remarks>
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

    /// <summary>
    /// Replaces the ApiKey with a masked form that preserves the first 4 characters and the
    /// original length. Matches the AzDO PAT masking pattern so the
    /// <c>UpdateAzureOpenAIConnectionCommand</c> handler's first-4-chars+length heuristic
    /// can detect an unchanged value when the user posts the masked form back unchanged.
    /// </summary>
    public void MaskApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey) && ApiKey.Length > 4)
            ApiKey = string.Concat(ApiKey.AsSpan(0, 4), new string('*', ApiKey.Length - 4));
    }
}
