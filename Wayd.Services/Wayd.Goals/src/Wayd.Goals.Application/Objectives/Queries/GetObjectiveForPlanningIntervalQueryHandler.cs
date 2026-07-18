using Wayd.Common.Application.Requests.Goals.Dtos;
using Wayd.Common.Application.Requests.Goals.Queries;
using Wayd.Goals.Application.Persistence;

namespace Wayd.Goals.Application.Objectives.Queries;

public sealed class GetObjectiveForPlanningIntervalQueryHandler(IGoalsDbContext goalsDbContext) : IQueryHandler<GetObjectiveForPlanningIntervalQuery, ObjectiveDetailsDto?>
{
    private readonly IGoalsDbContext _goalsDbContext = goalsDbContext;

    public async Task<ObjectiveDetailsDto?> Handle(GetObjectiveForPlanningIntervalQuery request, CancellationToken cancellationToken)
    {
        return await _goalsDbContext.Objectives
            .Where(o => o.Id == request.Id && o.PlanId == request.PlanningIntervalId)
            .ProjectToType<ObjectiveDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
