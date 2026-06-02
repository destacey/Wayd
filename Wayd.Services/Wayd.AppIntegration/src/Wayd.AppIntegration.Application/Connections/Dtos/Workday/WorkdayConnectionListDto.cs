using Mapster;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Workday;

public sealed record WorkdayConnectionListDto : ConnectionListDto, IMapFrom<WorkdayConnection>
{
    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkdayConnection, WorkdayConnectionListDto>()
            .Inherits<Connection, ConnectionListDto>();
    }
}
