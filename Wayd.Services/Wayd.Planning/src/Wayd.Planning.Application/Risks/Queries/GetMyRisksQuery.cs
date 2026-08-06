using Wayd.Planning.Application.Risks.Dtos;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.Risks.Queries;

public sealed record GetMyRisksQuery() : IQuery<IReadOnlyList<RiskListDto>>;

public sealed class GetMyRisksQueryHandler : IQueryHandler<GetMyRisksQuery, IReadOnlyList<RiskListDto>>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal;

    public GetMyRisksQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal)
    {
        _planningDbContext = planningDbContext;
        _currentPrincipal = currentPrincipal;
    }

    public async Task<IReadOnlyList<RiskListDto>> Handle(GetMyRisksQuery request, CancellationToken cancellationToken)
    {
        // Resolved rather than read from the token claim, which is a snapshot taken at sign-in: a user
        // linked mid-session would otherwise see an empty list until they signed in again. Empty
        // remains the honest answer for a genuinely unlinked account — nothing can be assigned to it.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            return [];

        return await _planningDbContext.Risks
            .Where(r => r.Status == RiskStatus.Open && r.AssigneeId == employeeId)
            .ProjectToType<RiskListDto>()
            .ToListAsync(cancellationToken);
    }
}
