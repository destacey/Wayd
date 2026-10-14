
namespace Wayd.Organization.Application.Persistence;

public interface IOrganizationDbContext : IWaydDbContext
{
    DbSet<BaseTeam> BaseTeams { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamOfTeams> TeamOfTeams { get; }
    DbSet<TeamOperatingModel> TeamOperatingModels { get; }
    DbSet<TeamMemberRole> TeamMemberRoles { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<HolidayCalendar> HolidayCalendars { get; }
    DbSet<EmployeeCapacity> EmployeeCapacities { get; }
    DbSet<EmployeeTeamAllocation> EmployeeTeamAllocations { get; }

    /// <summary>
    /// For reading time away across employees. Read-only: time away changes only through its
    /// <see cref="EmployeeCapacity"/>, which raises the events the activity log is built from.
    /// </summary>
    DbSet<EmployeeTimeAway> EmployeeTimeAway { get; }
}
