namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record TestAzureOpenAIConnectionRequest
{
    /// <summary>
    /// Azure OpenAI resource URL.
    /// </summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>
    /// The API key for Azure OpenAI resource.
    /// </summary>
    public string ApiKey { get; set; } = default!;

    /// <summary>
    /// The OpenAI model deployment name to test.
    /// </summary>
    public string DeploymentName { get; set; } = default!;
}
