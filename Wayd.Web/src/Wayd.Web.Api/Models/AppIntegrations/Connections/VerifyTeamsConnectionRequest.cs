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

    public VerifyTeamsConnectionCommand ToCommand()
        => new(TenantId, ClientId, ClientSecret);
}

public sealed class VerifyTeamsConnectionRequestValidator : CustomValidator<VerifyTeamsConnectionRequest>
{
    public VerifyTeamsConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(512);
    }
}
