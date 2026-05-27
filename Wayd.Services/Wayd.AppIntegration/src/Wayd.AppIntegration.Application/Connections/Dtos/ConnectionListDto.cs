using System.Text.Json.Serialization;
using Mapster;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos;

[JsonDerivedType(typeof(ConnectionListDto), typeDiscriminator: "connection")]
[JsonDerivedType(typeof(AzureDevOpsConnectionListDto), typeDiscriminator: "azure-devops")]
[JsonDerivedType(typeof(AzureOpenAIConnectionListDto), typeDiscriminator: "azure-openai")]
[JsonDerivedType(typeof(EntraConnectionListDto), typeDiscriminator: "entra")]
public record ConnectionListDto : IMapFrom<Connection>
{
    /// <summary>
    /// The unique identifier for the connection.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the connection.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The unique identifier for the system that this connection connects to.
    /// Only applicable to syncable connections (Work Management connectors).
    /// </summary>
    public string? SystemId { get; set; }

    /// <summary>
    /// The type of connector for the connection. This value cannot be changed once set.
    /// </summary>
    public required SimpleNavigationDto Connector { get; set; }

    /// <summary>
    /// The category of the connector which indicates the broader type of integration (e.g. "People Sync", "Work Management", "AI Service").
    /// </summary>
    public required SimpleNavigationDto Category { get; set; }

    /// <summary>
    /// Indicates whether the connection is active or not. Inactive connections are not included in operations.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// A flag indicating whether the connection configuration is valid.
    /// </summary>
    public bool IsValidConfiguration { get; set; }

    /// <summary>
    /// Indicates whether the connection can currently sync.
    /// Only applicable to syncable connections (Work Management connectors).
    /// </summary>
    public bool? CanSync { get; set; }

    public virtual void ConfigureMapping(TypeAdapterConfig config)
    {
        // Configure base mapping with derived type includes
        config.NewConfig<Connection, ConnectionListDto>()
            .Include<AzureDevOpsBoardsConnection, AzureDevOpsConnectionListDto>()
            .Include<AzureOpenAIConnection, AzureOpenAIConnectionListDto>()
            .Include<EntraConnection, EntraConnectionListDto>()
            .Map(dest => dest.Connector, src => SimpleNavigationDto.FromEnum(src.Connector))
            .Map(dest => dest.Category, src => SimpleNavigationDto.FromEnum(src.Connector.GetCategory()))
            .Map(dest => dest.SystemId, src => (src as ISyncableConnection) != null ? ((ISyncableConnection)src).SystemId : null)
            .Map(dest => dest.CanSync, src => (src as ISyncableConnection) != null ? ((ISyncableConnection)src).CanSync : (bool?)null);
    }
}
