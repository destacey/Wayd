using Mapster;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;

public sealed record AzureDevOpsConnectionDetailsDto : ConnectionDetailsDto, IMapFrom<AzureDevOpsBoardsConnection>
{

    /// <summary>
    /// The unique identifier for the external system that this connection connects to.
    /// </summary>
    public string? SystemId { get; set; }

    /// <summary>
    /// The configuration for the connection.
    /// </summary>
    public required AzureDevOpsConnectionConfigurationDto Configuration { get; set; }

    /// <summary>
    /// The configuration for the teams associated with the connection.
    /// </summary>
    public required AzureDevOpsTeamConfigurationDto TeamConfiguration { get; set; }

    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<AzureDevOpsBoardsConnection, AzureDevOpsConnectionDetailsDto>()
            .Inherits<Connection, ConnectionDetailsDto>();
    }
}
