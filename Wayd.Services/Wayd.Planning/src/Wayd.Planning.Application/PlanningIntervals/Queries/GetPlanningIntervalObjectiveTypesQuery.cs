using Wayd.Common.Extensions;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalObjectiveTypesQuery() : IQuery<IReadOnlyList<PlanningIntervalObjectiveTypeDto>>;

public sealed class GetPlanningIntervalObjectiveTypesQueryHandler : IQueryHandler<GetPlanningIntervalObjectiveTypesQuery, IReadOnlyList<PlanningIntervalObjectiveTypeDto>>
{
    public Task<IReadOnlyList<PlanningIntervalObjectiveTypeDto>> Handle(GetPlanningIntervalObjectiveTypesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanningIntervalObjectiveTypeDto> values = Enum.GetValues<PlanningIntervalObjectiveType>().Select(t => new PlanningIntervalObjectiveTypeDto
        {
            Id = (int)t,
            Name = t.GetDisplayName(),
            Order = t.GetDisplayOrder()
        }).ToList();

        return Task.FromResult(values);
    }
}
