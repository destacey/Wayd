namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// One employee gaining or losing one role, as carried by the PPM roles-changed events.
/// </summary>
/// <param name="Role">The role type id — the same key the events' role rosters use.</param>
/// <param name="EmployeeId">The employee who gained or lost it.</param>
public sealed record RoleAssignmentChange(int Role, Guid EmployeeId);
