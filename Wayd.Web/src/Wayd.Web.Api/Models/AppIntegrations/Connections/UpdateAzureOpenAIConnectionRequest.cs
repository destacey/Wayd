using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record UpdateAzureOpenAIConnectionRequest : UpdateConnectionRequest
{
    /// <summary>
    /// Azure OpenAI resource URL.
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// The API key for Azure OpenAI resource.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// The OpenAI model deployment name to use for this connection (e.g. "gpt-4o")
    /// </summary>
    public required string DeploymentName { get; set; }

    public UpdateAzureOpenAIConnectionCommand ToCommand()
        => new(Id, Name, BaseUrl, Description, DeploymentName, ApiKey);
}

public sealed class UpdateAzureOpenAIConnectionRequestValidator : CustomValidator<UpdateAzureOpenAIConnectionRequest>
{
    public UpdateAzureOpenAIConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new UpdateConnectionRequestValidator());

        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.ApiKey)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.DeploymentName)
            .NotEmpty()
            .MaximumLength(128);
    }
}
