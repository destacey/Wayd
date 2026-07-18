using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.StrategicManagement;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

public sealed record GetStrategicThemeStatesQuery : IQuery<List<StrategicThemeStateDto>> { }

public sealed class GetStrategicThemeStatesQueryHandler : IQueryHandler<GetStrategicThemeStatesQuery, List<StrategicThemeStateDto>>
{
    public Task<List<StrategicThemeStateDto>> Handle(GetStrategicThemeStatesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto.GetValues<StrategicThemeState, StrategicThemeStateDto>());
    }
}

public sealed record StrategicThemeStateDto : CommonEnumDto { }