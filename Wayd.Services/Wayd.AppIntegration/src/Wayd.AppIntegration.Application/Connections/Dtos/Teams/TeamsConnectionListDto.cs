using Mapster;
using Wayd.AppIntegration.Domain.Models.Teams;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Teams;

public sealed record TeamsConnectionListDto : ConnectionListDto, IMapFrom<TeamsConnection>
{
    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<TeamsConnection, TeamsConnectionListDto>()
            .Inherits<Connection, ConnectionListDto>();
    }
}
