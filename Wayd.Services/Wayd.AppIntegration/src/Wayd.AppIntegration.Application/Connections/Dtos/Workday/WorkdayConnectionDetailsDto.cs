using Mapster;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Workday;

public sealed record WorkdayConnectionDetailsDto : ConnectionDetailsDto, IMapFrom<WorkdayConnection>
{
    /// <summary>
    /// The configuration for the Workday connection.
    /// </summary>
    public required WorkdayConnectionConfigurationDto Configuration { get; set; }

    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<WorkdayConnection, WorkdayConnectionDetailsDto>()
            .Inherits<Connection, ConnectionDetailsDto>();
    }
}
