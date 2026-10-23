namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// Whether a time away request may be answered with "employee not found": true only when no employee has
/// <paramref name="EmployeeId"/> and the caller is allowed to manage that id's time away, which for an id
/// that matches no one means a time away administrator.
/// </summary>
/// <remarks>
/// Any other caller is refused by the time away commands' authorization check whether or not the employee
/// exists, so this answers false for them: they must not learn which ids exist.
/// </remarks>
public sealed record IsTimeAwayEmployeeMissingQuery(Guid EmployeeId) : IQuery<bool>;

/// <summary>Answers <see cref="IsTimeAwayEmployeeMissingQuery"/>, checking the caller's right to manage the id before whether it exists.</summary>
public sealed class IsTimeAwayEmployeeMissingQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<IsTimeAwayEmployeeMissingQuery, bool>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <summary>True only when the caller may manage the id's time away and no employee has it.</summary>
    public async Task<bool> Handle(IsTimeAwayEmployeeMissingQuery request, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.CanManageTimeAway(_organizationDbContext, request.EmployeeId, cancellationToken))
            return false;

        return !await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken);
    }
}
