using System.Linq.Expressions;
using Mapster;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Employees;

namespace Wayd.Common.Application.Employees.Queries;

public sealed record GetEmployeeQuery : IQuery<EmployeeDetailsDto?>
{
    public GetEmployeeQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Employee>();
    }

    public Expression<Func<Employee, bool>> IdOrKeyFilter { get; }
}

public sealed class GetEmployeeQueryHandler(
    IWaydDbContext waydDbContext)
    : IQueryHandler<GetEmployeeQuery, EmployeeDetailsDto?>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    public async Task<EmployeeDetailsDto?> Handle(GetEmployeeQuery request, CancellationToken cancellationToken)
    {
        return await _waydDbContext.Employees
            .Where(request.IdOrKeyFilter)
            .ProjectToType<EmployeeDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
