using Mapster;
using Wayd.AppIntegration.Domain.Models.Teams;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Teams;

public sealed record TeamsConnectionDetailsDto : ConnectionDetailsDto, IMapFrom<TeamsConnection>
{
    public required TeamsConnectionConfigurationDto Configuration { get; set; }

    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<TeamsConnection, TeamsConnectionDetailsDto>()
            .Inherits<Connection, ConnectionDetailsDto>();
    }
}
