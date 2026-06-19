using Mapster;
using Wayd.AppIntegration.Domain.Models.Entra;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Entra;

public sealed record EntraConnectionListDto : ConnectionListDto, IMapFrom<EntraConnection>
{
    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<EntraConnection, EntraConnectionListDto>()
            .Inherits<Connection, ConnectionListDto>();
    }
}
