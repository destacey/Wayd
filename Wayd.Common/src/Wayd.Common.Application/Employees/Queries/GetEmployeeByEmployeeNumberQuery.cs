using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

public sealed record GetEmployeeByEmployeeNumberQuery(string EmployeeNumber) : IQuery<Guid?>;

public sealed class GetEmployeeByEmployeeNumberQueryHandler : IQueryHandler<GetEmployeeByEmployeeNumberQuery, Guid?>
{
    private readonly IWaydDbContext _waydDbContext;

    public GetEmployeeByEmployeeNumberQueryHandler(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;
    }

    public async Task<Guid?> Handle(GetEmployeeByEmployeeNumberQuery request, CancellationToken cancellationToken)
    {
        // The Guid? cast is required: over a non-nullable Guid, FirstOrDefaultAsync returns Guid.Empty for
        // an unmatched number, which callers' HasValue checks accept as a real employee.
        return await _waydDbContext.Employees
            .Where(e => e.EmployeeNumber == request.EmployeeNumber)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
