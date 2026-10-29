using Wayd.AppIntegration.Application.Connections.Commands.Teams;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

/// <summary>
/// Credentials for verifying a Microsoft Teams connection before create.
/// </summary>
public sealed record VerifyTeamsConnectionRequest
{
    /// <summary>
    /// Entra directory tenant ID for the Teams communication app registration.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Application (client) ID for the Teams communication app registration.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Client secret for the Teams communication app registration.
    /// </summary>
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Entra access token from the signed-in user's OIDC session, exchanged on-behalf-of for Graph.
    /// </summary>
    public required string SubjectToken { get; set; }

    public VerifyTeamsConnectionCommand ToCommand()
        => new(TenantId, ClientId, ClientSecret, SubjectToken);
}

public sealed class VerifyTeamsConnectionRequestValidator : CustomValidator<VerifyTeamsConnectionRequest>
{
    public VerifyTeamsConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(512);
        RuleFor(x => x.SubjectToken)
            .NotEmpty()
            .WithMessage(
                "Microsoft Entra sign-in is required to verify a Teams connection. "
                + "Wayd could not obtain an Entra access token from your browser session — sign in with "
                + "Microsoft Entra ID and try again.");
    }
}
