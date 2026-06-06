using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;

namespace Wayd.ProjectPortfolioManagement.Application;

public interface IProjectPortfolioManagementDbContext : IWaydDbContext
{
    DbSet<ExpenditureCategory> ExpenditureCategories { get; }
    DbSet<ProjectPortfolio> Portfolios { get; }
    DbSet<Program> Programs { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectHealthCheck> ProjectHealthChecks { get; }
    DbSet<ProjectScore> ProjectScores { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<ProjectTaskDependency> ProjectTaskDependencies { get; }
    DbSet<PpmTeam> PpmTeams { get; }
    DbSet<StrategicTheme> PpmStrategicThemes { get; }
    DbSet<StrategicInitiative> StrategicInitiatives { get; }
    DbSet<ProjectLifecycle> ProjectLifecycles { get; }
    DbSet<ProjectPhase> ProjectPhases { get; }
}
