using Mapster;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Employees;

namespace Wayd.Common.Application.Employees.Queries;

public sealed record GetEmployeeNumberMapQuery : IQuery<IReadOnlyList<EmployeeNumberMapDto>>
{
    public GetEmployeeNumberMapQuery(bool includeInactive = false)
    {
        IncludeInactive = includeInactive;
    }

    public bool IncludeInactive { get; }
}

public sealed class GetEmployeeNumberMapQueryHandler : IQueryHandler<GetEmployeeNumberMapQuery, IReadOnlyList<EmployeeNumberMapDto>>
{
    private readonly IWaydDbContext _waydDbContext;

    public GetEmployeeNumberMapQueryHandler(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;
    }

    public async Task<IReadOnlyList<EmployeeNumberMapDto>> Handle(GetEmployeeNumberMapQuery request, CancellationToken cancellationToken)
    {
        var query = _waydDbContext.Employees.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(e => e.IsActive);

        return await query.ProjectToType<EmployeeNumberMapDto>().ToListAsync(cancellationToken);
    }
}

public sealed record EmployeeNumberMapDto : IMapFrom<Employee>
{
    /// <summary>Gets the identifier.</summary>
    /// <value>The identifier.</value>
    public Guid Id { get; set; }

    /// <summary>Gets the employee number.</summary>
    /// <value>The employee number.</value>
    public required string EmployeeNumber { get; set; }

    /// <summary>
    /// Indicates whether the employee is active or not.  
    /// </summary>
    public bool IsActive { get; set; }
}
