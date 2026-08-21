using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Employees.Queries;

/// <summary>
/// Ids of every employee a command may legitimately reference. Lets a handler validate an incoming
/// employee id without trusting the wire, matching how team mappings are validated.
/// </summary>
public sealed record GetValidEmployeeIdsQuery() : IQuery<Guid[]>;

public sealed class GetValidEmployeeIdsQueryHandler(IWaydDbContext waydDbContext) : IQueryHandler<GetValidEmployeeIdsQuery, Guid[]>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;

    public async Task<Guid[]> Handle(GetValidEmployeeIdsQuery request, CancellationToken cancellationToken)
    {
        // Inactive employees are included: a former employee still authored and was assigned work
        // items, so an admin must be able to attribute historical work to them.
        return await _waydDbContext.Employees
            .Select(e => e.Id)
            .ToArrayAsync(cancellationToken);
    }
}
