using System.Text.Json.Serialization;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

[JsonDerivedType(typeof(CreateAzureDevOpsConnectionRequest), typeDiscriminator: "azure-devops")]
[JsonDerivedType(typeof(CreateAzureOpenAIConnectionRequest), typeDiscriminator: "azure-openai")]
[JsonDerivedType(typeof(CreateEntraConnectionRequest), typeDiscriminator: "entra")]
// Note: OpenAI discriminator reserved for future implementation
// [JsonDerivedType(typeof(CreateOpenAIConnectionRequest), typeDiscriminator: "openai")]
public abstract record CreateConnectionRequest
{
    /// <summary>
    /// The name of the connection.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The description of the connection.
    /// </summary>
    public string? Description { get; set; }
}

public class CreateConnectionRequestValidator : CustomValidator<CreateConnectionRequest>
{
    public CreateConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}
