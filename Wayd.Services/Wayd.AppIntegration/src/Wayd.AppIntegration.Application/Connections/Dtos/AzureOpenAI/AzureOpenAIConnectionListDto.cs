using Mapster;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;

namespace Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;

public sealed record AzureOpenAIConnectionListDto : ConnectionListDto, IMapFrom<AzureOpenAIConnection>
{
    public override void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<AzureOpenAIConnection, AzureOpenAIConnectionListDto>()
            .Inherits<Connection, ConnectionListDto>();
    }
}
