using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;

namespace Wayd.DemandManagement.Application.IntakeDesks.Queries;

/// <summary>
/// The desks an employee is named on and the open requests they raised; empty for an employee with neither.
/// </summary>
/// <remarks>
/// Counts the employee's own involvement only, so it applies no reader visibility: every request counted is one
/// the employee requested or submitted, which they can always see.
/// </remarks>
public sealed record GetEmployeeIntakeInvolvementQuery(Guid EmployeeId) : IQuery<EmployeeIntakeInvolvementDto>;

public sealed class GetEmployeeIntakeInvolvementQueryHandler(IDemandManagementDbContext demandManagementDbContext)
    : IQueryHandler<GetEmployeeIntakeInvolvementQuery, EmployeeIntakeInvolvementDto>
{
    private readonly IDemandManagementDbContext _demandManagementDbContext = demandManagementDbContext;

    public async Task<EmployeeIntakeInvolvementDto> Handle(GetEmployeeIntakeInvolvementQuery request, CancellationToken cancellationToken)
    {
        var employeeId = request.EmployeeId;

        var roles = await _demandManagementDbContext.IntakeDesks
            .SelectMany(d => d.Members)
            .Where(m => m.EmployeeId == employeeId)
            .Select(m => m.Role)
            .ToListAsync(cancellationToken);

        var openRequestsRaised = await _demandManagementDbContext.IntakeRequests
            .Where(r => r.RequesterId == employeeId || r.SubmittedById == employeeId)
            .CountAsync(r => r.StatusCategory == StatusCategory.Proposed || r.StatusCategory == StatusCategory.Active, cancellationToken);

        return new EmployeeIntakeInvolvementDto
        {
            DeskCount = roles.Count,
            AdminDeskCount = roles.Count(r => r == IntakeDeskRole.Admin),
            AgentDeskCount = roles.Count(r => r == IntakeDeskRole.Agent),
            OpenRequestsRaisedCount = openRequestsRaised,
        };
    }
}
