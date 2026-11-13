using Wayd.Common.Application.Requests.Planning;
using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.DemandManagement.Application.IntakeDesks;

/// <summary>
/// Tells Planning which intake desks an employee works, so every agent and Desk Admin on a desk facilitates the
/// poker sessions it hosts.
/// </summary>
/// <remarks>
/// Membership alone decides it. The Demand Management Admin acts on every desk's requests without being a member,
/// but runs a desk's sessions only once named on the desk, since Planning asks about an employee, not a claim.
/// </remarks>
public sealed class IntakeDeskPokerSessionHostAccess(IDemandManagementDbContext dbContext) : IPokerSessionHostAccess
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public PokerSessionHostKind Kind => PokerSessionHostKind.IntakeDesk;

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> HostsManagedBy(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Employees.AnyAsync(e => e.Id == employeeId && e.IsActive, cancellationToken))
            return new HashSet<Guid>();

        var deskIds = await _dbContext.IntakeDesks
            .SelectMany(d => d.Members)
            .Where(m => m.EmployeeId == employeeId)
            .Select(m => m.DeskId)
            .ToListAsync(cancellationToken);

        return deskIds.ToHashSet();
    }
}
