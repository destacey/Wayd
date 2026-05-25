using Mapster;
using Wayd.AppIntegration.Domain.Models.Entra;

namespace Wayd.AppIntegration.Application.Connections.Dtos.Entra;

public sealed record EntraConnectionDetailsDto : ConnectionDetailsDto, IMapFrom<EntraConnection>
{
    /// <summary>
    /// The configuration for the Entra connection.
    /// </summary>
    public required EntraConnectionConfigurationDto Configuration { get; set; }

    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<EntraConnection, EntraConnectionDetailsDto>()
            .Inherits<Connection, ConnectionDetailsDto>();
    }
}
