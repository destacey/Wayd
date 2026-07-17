using Wayd.Common.Extensions;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalObjectiveStatusesQuery : IQuery<IReadOnlyList<PlanningIntervalObjectiveStatusDto>> { }

public sealed class GetPlanningIntervalObjectiveStatusesQueryHandler : IQueryHandler<GetPlanningIntervalObjectiveStatusesQuery, IReadOnlyList<PlanningIntervalObjectiveStatusDto>>
{
    public Task<IReadOnlyList<PlanningIntervalObjectiveStatusDto>> Handle(GetPlanningIntervalObjectiveStatusesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanningIntervalObjectiveStatusDto> values = Enum.GetValues<ObjectiveStatus>().Select(c => new PlanningIntervalObjectiveStatusDto
        {
            Id = (int)c,
            Name = c.GetDisplayName(),
            Description = c.GetDisplayDescription(),
            Order = c.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
