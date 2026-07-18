using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapStatesQuery : IQuery<List<RoadmapStateDto>> { }

public sealed class GetRoadmapStatesQueryHandler : IQueryHandler<GetRoadmapStatesQuery, List<RoadmapStateDto>>
{
    public Task<List<RoadmapStateDto>> Handle(GetRoadmapStatesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommonEnumDto.GetValues<RoadmapState, RoadmapStateDto>());
    }
}

public sealed record RoadmapStateDto : CommonEnumDto { }
