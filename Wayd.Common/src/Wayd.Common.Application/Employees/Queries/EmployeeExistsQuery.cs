using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

/// <summary>
/// Whether an employee, active or not, has <paramref name="EmployeeId"/>. Lets an endpoint answer 404 for an
/// unknown employee before dispatching a command whose failure would otherwise read as a bad request.
/// </summary>
public sealed record EmployeeExistsQuery(Guid EmployeeId) : IQuery<bool>;

/// <summary>Answers <see cref="EmployeeExistsQuery"/> from the employees table.</summary>
public sealed class EmployeeExistsQueryHandler(IWaydDbContext waydDbContext) : IQueryHandler<EmployeeExistsQuery, bool>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    /// <summary>Whether any employee, active or inactive, has the requested id.</summary>
    public async Task<bool> Handle(EmployeeExistsQuery request, CancellationToken cancellationToken)
    {
        return await _waydDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken);
    }
}
