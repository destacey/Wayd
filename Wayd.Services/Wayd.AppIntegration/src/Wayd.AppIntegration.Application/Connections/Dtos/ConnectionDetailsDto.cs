using System.Text.Json.Serialization;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Application.Connections.Dtos.Workday;
using Wayd.AppIntegration.Application.Connectors.Dtos;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connections.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
//[JsonDerivedType(typeof(ConnectionDetailsDto), typeDiscriminator: "connection")]
[JsonDerivedType(typeof(AzureDevOpsConnectionDetailsDto), typeDiscriminator: "azure-devops")]
[JsonDerivedType(typeof(AzureOpenAIConnectionDetailsDto), typeDiscriminator: "azure-openai")]
[JsonDerivedType(typeof(EntraConnectionDetailsDto), typeDiscriminator: "entra")]
[JsonDerivedType(typeof(WorkdayConnectionDetailsDto), typeDiscriminator: "workday")]
public record ConnectionDetailsDto : IMapFrom<Connection>
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
    /// The connection description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The type of connector for the connection. This value cannot be changed once set.
    /// </summary>
    public required SimpleNavigationDto Connector { get; set; }

    /// <summary>
    /// The capabilities this connection's connector supports, each with its display category.
    /// </summary>
    public IReadOnlyList<ConnectorCapabilityDto> Capabilities { get; set; } = [];

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
    /// Requires: IsActive and IsValidConfiguration and HasActiveIntegrationObjects
    /// </summary>
    public bool? CanSync { get; set; }

    public virtual void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<Connection, ConnectionDetailsDto>()
            .Include<AzureDevOpsBoardsConnection, AzureDevOpsConnectionDetailsDto>()
            .Include<AzureOpenAIConnection, AzureOpenAIConnectionDetailsDto>()
            .Include<EntraConnection, EntraConnectionDetailsDto>()
            .Include<WorkdayConnection, WorkdayConnectionDetailsDto>()
            .Map(dest => dest.Connector, src => SimpleNavigationDto.FromEnum(src.Connector))
            .Map(dest => dest.Capabilities, src => src.Connector.GetCapabilities().Select(ConnectorCapabilityDto.FromEnum).ToList())
            .Map(dest => dest.CanSync, src => (src as ISyncableConnection) != null ? ((ISyncableConnection)src).CanSync : (bool?)null);
    }
}
