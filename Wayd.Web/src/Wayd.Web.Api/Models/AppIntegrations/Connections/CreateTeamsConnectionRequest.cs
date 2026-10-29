using Wayd.AppIntegration.Application.Connections.Commands.Teams;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record CreateTeamsConnectionRequest : CreateConnectionRequest
{
    public required string TenantId { get; set; }

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    /// <summary>
    /// Entra access token from the signed-in user's OIDC session for the hello-world chat message.
    /// </summary>
    public required string SubjectToken { get; set; }

    public CreateTeamsConnectionCommand ToCommand()
        => new(Name, Description, TenantId, ClientId, ClientSecret, SubjectToken);
}

public sealed class CreateTeamsConnectionRequestValidator : CustomValidator<CreateTeamsConnectionRequest>
{
    public CreateTeamsConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new CreateConnectionRequestValidator());

        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(512);
        RuleFor(x => x.SubjectToken)
            .NotEmpty()
            .WithMessage(
                "Microsoft Entra sign-in is required to create a Teams connection. "
                + "Wayd could not obtain an Entra access token from your browser session — sign in with "
                + "Microsoft Entra ID and try again.");
    }
}
