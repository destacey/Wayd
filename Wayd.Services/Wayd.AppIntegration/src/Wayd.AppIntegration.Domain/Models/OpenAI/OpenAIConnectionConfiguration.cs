using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Wayd.Common.Domain.DataProtection;

namespace Wayd.AppIntegration.Domain.Models.OpenAI;

public sealed class OpenAIConnectionConfiguration
{
    [JsonConstructor]
    private OpenAIConnectionConfiguration() { }

    [method: SetsRequiredMembers]
    public OpenAIConnectionConfiguration(string apiKey, string modelName, string baseUrl = "https://api.openai.com/v1", double defaultTemperature = 0.1, int defaultMaxOutputTokens = 400, bool jsonModePreferred = true)
    {
        ApiKey = apiKey.Trim();
        ModelName = modelName.Trim();
        BaseUrl = baseUrl?.Trim() ?? "https://api.openai.com/v1";
        DefaultTemperature = defaultTemperature;
        DefaultMaxOutputTokens = defaultMaxOutputTokens;
        JsonModePreferred = jsonModePreferred;
        ConfigVersion = 1;
    }

    /// <summary>
    /// Gets or sets the API key for Authorization: Bearer header to OpenAI.
    /// </summary>
    [Encrypted]
    public required string ApiKey { get; set; }

    /// <summary>
    /// OpenAI base URL. Defaults to "https://api.openai.com/v1".
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// The OpenAI model name to use for this connection (e.g. "gpt-4o")
    /// </summary>
    public required string ModelName { get; set; }

    /// <summary>
    /// OpenAI-Organization header gets set to this value
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// OpenAI-Project header gets set to this value
    /// </summary>
    public string? ProjectId { get; set; }

    #region Common AI Settings

    public double DefaultTemperature { get; set; }

    public int DefaultMaxOutputTokens { get; set; }

    public bool JsonModePreferred { get; set; }

    #endregion Common AI Settings

    #region Inferred Properties
    public (string Name, string Value) AuthorizationHeader => ("Authorization", $"Bearer {ApiKey}");
    public (string Name, string Value) OrganizationHeader => !string.IsNullOrWhiteSpace(OrganizationId) ? ("OpenAI-Organization", OrganizationId) : (string.Empty, string.Empty);
    public (string Name, string Value) ProjectHeader => !string.IsNullOrWhiteSpace(ProjectId) ? ("OpenAI-Project", ProjectId) : (string.Empty, string.Empty);
    public IEnumerable<(string Name, string Value)> Headers
        => new List<(string Name, string Value)>
        {
            AuthorizationHeader,
            OrganizationHeader,
            ProjectHeader
        }
        .Where(h => !string.IsNullOrWhiteSpace(h.Name));

    #endregion Inferred Properties

    public int ConfigVersion { get; init; }
}