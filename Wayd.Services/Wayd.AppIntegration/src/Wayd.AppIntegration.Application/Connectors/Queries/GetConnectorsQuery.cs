using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.AppIntegration.Application.Connectors.Queries;

public sealed record GetConnectorsQuery : IQuery<IReadOnlyList<ConnectorListDto>> { }

public sealed class GetConnectorsQueryHandler : IQueryHandler<GetConnectorsQuery, IReadOnlyList<ConnectorListDto>>
{
    public Task<IReadOnlyList<ConnectorListDto>> Handle(GetConnectorsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ConnectorListDto> values = [.. Enum.GetValues<Connector>().Select(c => new ConnectorListDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Capabilities = [.. c.GetCapabilities().Select(ConnectorCapabilityDto.FromEnum)],
        })];

        return Task.FromResult(values);
    }
}
