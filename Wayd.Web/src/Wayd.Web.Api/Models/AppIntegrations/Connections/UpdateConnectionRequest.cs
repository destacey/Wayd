using System.Text.Json.Serialization;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

[JsonDerivedType(typeof(UpdateAzureDevOpsConnectionRequest), typeDiscriminator: "azure-devops")]
[JsonDerivedType(typeof(UpdateAzureOpenAIConnectionRequest), typeDiscriminator: "azure-openai")]
[JsonDerivedType(typeof(UpdateEntraConnectionRequest), typeDiscriminator: "entra")]
[JsonDerivedType(typeof(UpdateWorkdayConnectionRequest), typeDiscriminator: "workday")]
// Note: OpenAI discriminator reserved for future implementation
// [JsonDerivedType(typeof(UpdateOpenAIConnectionRequest), typeDiscriminator: "openai")]
public abstract record UpdateConnectionRequest
{
    /// <summary>
    /// The unique identifier for the connection.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the connection.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The description of the connection.
    /// </summary>
    public string? Description { get; set; }
}

public class UpdateConnectionRequestValidator : CustomValidator<UpdateConnectionRequest>
{
    public UpdateConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}
