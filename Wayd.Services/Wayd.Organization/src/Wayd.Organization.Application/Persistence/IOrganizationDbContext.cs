
namespace Wayd.Organization.Application.Persistence;

public interface IOrganizationDbContext : IWaydDbContext
{
    DbSet<BaseTeam> BaseTeams { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamOfTeams> TeamOfTeams { get; }
    DbSet<TeamOperatingModel> TeamOperatingModels { get; }
    DbSet<TeamMemberRole> TeamMemberRoles { get; }
    DbSet<TeamMember> TeamMembers { get; }
}
