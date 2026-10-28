using Wayd.AppIntegration.Application.Connections.Commands.Teams;

namespace Wayd.Web.Api.Models.AppIntegrations.Connections;

public sealed record CreateTeamsConnectionRequest : CreateConnectionRequest
{
    public required string TenantId { get; set; }

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    public CreateTeamsConnectionCommand ToCommand()
        => new(Name, Description, TenantId, ClientId, ClientSecret);
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
    }
}
