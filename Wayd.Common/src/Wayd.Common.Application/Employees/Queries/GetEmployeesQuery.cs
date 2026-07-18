using Mapster;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

public sealed record GetEmployeesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<EmployeeListDto>>;

public sealed class GetEmployeesQueryHandler : IQueryHandler<GetEmployeesQuery, IReadOnlyList<EmployeeListDto>>
{
    private readonly IWaydDbContext _waydDbContext;

    public GetEmployeesQueryHandler(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;
    }

    public async Task<IReadOnlyList<EmployeeListDto>> Handle(GetEmployeesQuery request, CancellationToken cancellationToken)
    {
        var query = _waydDbContext.Employees
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        return await query.ProjectToType<EmployeeListDto>().ToListAsync(cancellationToken);
    }
}
