using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Enums;

namespace Wayd.StrategicManagement.Application.Visions.Queries;

public sealed record GetVisionStatesQuery : IQuery<List<VisionStateDto>> { }

public sealed class GetVisionStatesQueryHandler : IQueryHandler<GetVisionStatesQuery, List<VisionStateDto>>
{
    public Task<List<VisionStateDto>> Handle(GetVisionStatesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto.GetValues<VisionState, VisionStateDto>());
    }
}

public sealed record VisionStateDto : CommonEnumDto { }