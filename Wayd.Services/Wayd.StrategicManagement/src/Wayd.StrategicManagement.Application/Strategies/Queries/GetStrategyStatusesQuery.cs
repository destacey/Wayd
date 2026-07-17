using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Enums;

namespace Wayd.StrategicManagement.Application.Strategies.Queries;

public sealed record GetStrategyStatusesQuery : IQuery<List<StrategyStatusDto>> { }

public sealed class GetStrategyStatusesQueryHandler : IQueryHandler<GetStrategyStatusesQuery, List<StrategyStatusDto>>
{
    public Task<List<StrategyStatusDto>> Handle(GetStrategyStatusesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto.GetValues<StrategyStatus, StrategyStatusDto>());
    }
}

public sealed record StrategyStatusDto : CommonEnumDto { }