using Wayd.AppIntegration.Application.Connections.Commands.Teams;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record UpdateTeamsConnectionRequest : UpdateConnectionRequest
{
    public required string TenantId { get; set; }

    public required string ClientId { get; set; }

    /// <summary>
    /// The client secret for the Entra app registration. Leave blank to keep the stored value, unless the
    /// update changes the tenant or client ID, which needs it entered again.
    /// </summary>
    /// <remarks>
    /// A stored credential is only sent where it was entered for. There is no way to clear it: the connection
    /// cannot function without a credential, so removal means deleting the connection.
    /// </remarks>
    public string? ClientSecret { get; set; }

    public UpdateTeamsConnectionCommand ToCommand()
        => new(Id, Name, Description, TenantId, ClientId, ClientSecret);
}

public sealed class UpdateTeamsConnectionRequestValidator : CustomValidator<UpdateTeamsConnectionRequest>
{
    public UpdateTeamsConnectionRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        Include(new UpdateConnectionRequestValidator());

        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ClientSecret).MaximumLength(512);
    }
}
