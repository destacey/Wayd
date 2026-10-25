using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.DemandManagement.Application.Employees.Queries;

/// <summary>
/// The active employees a request can name — as requester, participant or whoever made a commitment — by
/// name only.
/// </summary>
/// <remarks>
/// Intake is open to anyone in the company, most of whom cannot list employees through Organization, which
/// needs View on employees. This returns no more than a request already shows about the people it names.
/// </remarks>
public sealed record GetIntakeEmployeeOptionsQuery : IQuery<List<EmployeeNavigationDto>>;

public sealed class GetIntakeEmployeeOptionsQueryHandler(IDemandManagementDbContext dbContext)
    : IQueryHandler<GetIntakeEmployeeOptionsQuery, List<EmployeeNavigationDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    public async Task<List<EmployeeNavigationDto>> Handle(GetIntakeEmployeeOptionsQuery request, CancellationToken cancellationToken) =>
        await _dbContext.Employees
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name.FirstName).ThenBy(e => e.Name.LastName).ThenBy(e => e.Id)
            .Select(e => new EmployeeNavigationDto { Id = e.Id, Key = e.Key, Name = e.Name.FirstName + " " + e.Name.LastName })
            .ToListAsync(cancellationToken);
}
