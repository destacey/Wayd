using Wayd.AppIntegration.Application.Connections.Commands.Teams;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record UpdateTeamsConnectionRequest : UpdateConnectionRequest
{
    public required string TenantId { get; set; }

    public required string ClientId { get; set; }

    /// <remarks>Leave blank to keep the stored value.</remarks>
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
