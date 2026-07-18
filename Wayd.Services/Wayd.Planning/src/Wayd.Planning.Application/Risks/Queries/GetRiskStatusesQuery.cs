using Wayd.Common.Extensions;
using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetRiskStatusesQuery : IQuery<IReadOnlyList<RiskStatusDto>> { }

public sealed class GetRiskStatusesQueryHandler : IQueryHandler<GetRiskStatusesQuery, IReadOnlyList<RiskStatusDto>>
{
    public Task<IReadOnlyList<RiskStatusDto>> Handle(GetRiskStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RiskStatusDto> values = Enum.GetValues<RiskStatus>().Select(c => new RiskStatusDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
