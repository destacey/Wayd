using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record UpdateAzureDevOpsConnectionRequest : UpdateConnectionRequest
{
    /// <summary>
    /// The Azure DevOps Organization name.
    /// </summary>
    public required string Organization { get; set; }

    /// <summary>
    /// The personal access token that enables access to Azure DevOps data. Leave blank to keep the stored
    /// value, unless the update changes the organization, which needs it entered again.
    /// </summary>
    /// <remarks>
    /// A stored credential is only sent where it was entered for. There is no way to clear it: the connection
    /// cannot function without a credential, so removal means deleting the connection.
    /// </remarks>
    public string? PersonalAccessToken { get; set; }

    public UpdateAzureDevOpsConnectionCommand ToCommand()
        => new(Id, Name, Description, Organization, PersonalAccessToken);
}

public sealed class UpdateAzureDevOpsConnectionRequestValidator : CustomValidator<UpdateAzureDevOpsConnectionRequest>
{
    public UpdateAzureDevOpsConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new UpdateConnectionRequestValidator());

        RuleFor(c => c.Organization)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.PersonalAccessToken)
            .MaximumLength(128);
    }
}
