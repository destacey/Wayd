using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetMyRisksQuery() : IQuery<IReadOnlyList<RiskListDto>>;

public sealed class GetMyRisksQueryHandler : IQueryHandler<GetMyRisksQuery, IReadOnlyList<RiskListDto>>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ICurrentUser _currentUser;

    public GetMyRisksQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser)
    {
        _planningDbContext = planningDbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RiskListDto>> Handle(GetMyRisksQuery request, CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.GetEmployeeId();
        if (employeeId is null)
            return [];

        return await _planningDbContext.Risks
            .Where(r => r.Status == RiskStatus.Open && r.AssigneeId == employeeId)
            .ProjectToType<RiskListDto>()
            .ToListAsync(cancellationToken);
    }
}
