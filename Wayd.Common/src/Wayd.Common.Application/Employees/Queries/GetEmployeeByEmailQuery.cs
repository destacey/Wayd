using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

/// <summary>
/// Looks up an Employee by email (case-insensitive). Used by the User-to-Employee link path —
/// auth identity is keyed on email regardless of which PeopleSync connector is currently active.
/// </summary>
public sealed record GetEmployeeByEmailQuery(string Email) : IQuery<Guid?>;

internal sealed class GetEmployeeByEmailQueryHandler : IQueryHandler<GetEmployeeByEmailQuery, Guid?>
{
    private readonly IWaydDbContext _waydDbContext;

    public GetEmployeeByEmailQueryHandler(IWaydDbContext waydDbContext)
    {
        _waydDbContext = waydDbContext;
    }

    public async Task<Guid?> Handle(GetEmployeeByEmailQuery request, CancellationToken cancellationToken)
    {
        return await _waydDbContext.Employees
            .Where(e => e.Email.Value == request.Email)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
