using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Risks.Queries;

/// <summary>
/// How many open risks are assigned to an employee: the risks <see cref="GetMyRisksQuery"/> lists for them.
/// </summary>
public sealed record GetEmployeeOpenRiskCountQuery(Guid EmployeeId) : IQuery<int>;

public sealed class GetEmployeeOpenRiskCountQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetEmployeeOpenRiskCountQuery, int>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public Task<int> Handle(GetEmployeeOpenRiskCountQuery request, CancellationToken cancellationToken) =>
        _planningDbContext.Risks
            .CountAsync(r => r.Status == RiskStatus.Open && r.AssigneeId == request.EmployeeId, cancellationToken);
}
