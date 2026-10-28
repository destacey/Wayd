using Wayd.AppIntegration.Domain.Models.Teams;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Teams;

public sealed record TeamsConnectionConfigurationDto : IMapFrom<TeamsConnectionConfiguration>
{
    public required string TenantId { get; set; }

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    public void MaskClientSecret()
        => ClientSecret = ConnectionSecret.Masked(ClientSecret);
}
