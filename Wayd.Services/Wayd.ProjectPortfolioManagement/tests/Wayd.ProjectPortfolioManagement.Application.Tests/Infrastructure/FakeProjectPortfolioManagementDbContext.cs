using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IProjectPortfolioManagementDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all ProjectPortfolioManagement domain tests.
/// </summary>
public class FakeProjectPortfolioManagementDbContext : IProjectPortfolioManagementDbContext, IDisposable
{
    // ProjectPortfolioManagement domain entities
    private readonly List<ExpenditureCategory> _expenditureCategories = [];
    private readonly List<ProjectPortfolio> _portfolios = [];
    private readonly List<Program> _programs = [];
    private readonly List<Project> _projects = [];
    private readonly List<ProjectHealthCheck> _projectHealthChecks = [];
    private readonly List<ProjectTask> _projectTasks = [];
    private readonly List<ProjectTaskDependency> _projectTaskDependencies = [];
    private readonly List<PpmTeam> _ppmTeams = [];
    private readonly List<StrategicTheme> _ppmStrategicThemes = [];
    private readonly List<StrategicInitiative> _strategicInitiatives = [];
    private readonly List<ProjectLifecycle> _projectLifecycles = [];
    private readonly List<ProjectPhase> _projectPhases = [];
    private readonly List<ScoringModel> _scoringModels = [];

    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];

    // DbSet properties
    public DbSet<ExpenditureCategory> ExpenditureCategories => _expenditureCategories.AsDbSet();
    public DbSet<ProjectPortfolio> Portfolios => _portfolios.AsDbSet();
    public DbSet<Program> Programs => _programs.AsDbSet();
    public DbSet<Project> Projects => _projects.AsDbSet();
    public DbSet<ProjectHealthCheck> ProjectHealthChecks => _projectHealthChecks.AsDbSet();
    public DbSet<ProjectTask> ProjectTasks => _projectTasks.AsDbSet();
    public DbSet<ProjectTaskDependency> ProjectTaskDependencies => _projectTaskDependencies.AsDbSet();
    public DbSet<PpmTeam> PpmTeams => _ppmTeams.AsDbSet();
    public DbSet<StrategicTheme> PpmStrategicThemes => _ppmStrategicThemes.AsDbSet();
    public DbSet<StrategicInitiative> StrategicInitiatives => _strategicInitiatives.AsDbSet();
    public DbSet<ProjectLifecycle> ProjectLifecycles => _projectLifecycles.AsDbSet();
    public DbSet<ProjectPhase> ProjectPhases => _projectPhases.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakeProjectPortfolioManagementDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Return the total number of entities as a simple success indicator
        var count = _expenditureCategories.Count + _portfolios.Count + _programs.Count +
                    _projects.Count + _projectTasks.Count + _projectTaskDependencies.Count +
                    _ppmTeams.Count + _ppmStrategicThemes.Count + _strategicInitiatives.Count +
                    _employees.Count + _externalEmployeeBlacklistItems.Count + _personalAccessTokens.Count;
        return Task.FromResult(count);
    }

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
    {
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    public EntityEntry Entry(object entity)
    {
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    #region Helper Methods for Test Setup

    // ExpenditureCategory
    public void AddExpenditureCategory(ExpenditureCategory category) => _expenditureCategories.Add(category);
    public void AddExpenditureCategories(IEnumerable<ExpenditureCategory> categories) => _expenditureCategories.AddRange(categories);

    // ProjectPortfolio
    public void AddPortfolio(ProjectPortfolio portfolio) => _portfolios.Add(portfolio);
    public void AddPortfolios(IEnumerable<ProjectPortfolio> portfolios) => _portfolios.AddRange(portfolios);

    // Program
    public void AddProgram(Program program) => _programs.Add(program);
    public void AddPrograms(IEnumerable<Program> programs) => _programs.AddRange(programs);

    // Project
    public void AddProject(Project project) => _projects.Add(project);
    public void AddProjects(IEnumerable<Project> projects) => _projects.AddRange(projects);

    // ProjectHealthCheck
    public void AddProjectHealthCheck(ProjectHealthCheck healthCheck) => _projectHealthChecks.Add(healthCheck);
    public void AddProjectHealthChecks(IEnumerable<ProjectHealthCheck> healthChecks) => _projectHealthChecks.AddRange(healthChecks);

    // ProjectTask
    public void AddProjectTask(ProjectTask task) => _projectTasks.Add(task);
    public void AddProjectTasks(IEnumerable<ProjectTask> tasks) => _projectTasks.AddRange(tasks);

    // ProjectTaskDependency
    public void AddProjectTaskDependency(ProjectTaskDependency dependency) => _projectTaskDependencies.Add(dependency);
    public void AddProjectTaskDependencies(IEnumerable<ProjectTaskDependency> dependencies) => _projectTaskDependencies.AddRange(dependencies);

    // PpmTeam
    public void AddPpmTeam(PpmTeam team) => _ppmTeams.Add(team);
    public void AddPpmTeams(IEnumerable<PpmTeam> teams) => _ppmTeams.AddRange(teams);

    // StrategicTheme (PPM)
    public void AddPpmStrategicTheme(StrategicTheme theme) => _ppmStrategicThemes.Add(theme);
    public void AddPpmStrategicThemes(IEnumerable<StrategicTheme> themes) => _ppmStrategicThemes.AddRange(themes);

    // StrategicInitiative
    public void AddStrategicInitiative(StrategicInitiative initiative) => _strategicInitiatives.Add(initiative);
    public void AddStrategicInitiatives(IEnumerable<StrategicInitiative> initiatives) => _strategicInitiatives.AddRange(initiatives);

    // ProjectLifecycle
    public void AddProjectLifecycle(ProjectLifecycle lifecycle) => _projectLifecycles.Add(lifecycle);
    public void AddProjectLifecycles(IEnumerable<ProjectLifecycle> lifecycles) => _projectLifecycles.AddRange(lifecycles);

    // ProjectPhase
    public void AddProjectPhase(ProjectPhase phase) => _projectPhases.Add(phase);
    public void AddProjectPhases(IEnumerable<ProjectPhase> phases) => _projectPhases.AddRange(phases);

    // ScoringModel
    public void AddScoringModel(ScoringModel model) => _scoringModels.Add(model);
    public void AddScoringModels(IEnumerable<ScoringModel> models) => _scoringModels.AddRange(models);

    // Employee
    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddEmployees(IEnumerable<Employee> employees) => _employees.AddRange(employees);

    /// <summary>
    /// Clears all entities from the fake context and resets counters.
    /// Useful for test cleanup or test isolation.
    /// </summary>
    public void Clear()
    {
        _expenditureCategories.Clear();
        _portfolios.Clear();
        _programs.Clear();
        _projects.Clear();
        _projectHealthChecks.Clear();
        _projectTasks.Clear();
        _projectTaskDependencies.Clear();
        _ppmTeams.Clear();
        _ppmStrategicThemes.Clear();
        _strategicInitiatives.Clear();
        _projectLifecycles.Clear();
        _projectPhases.Clear();
        _scoringModels.Clear();
        _employees.Clear();
        _externalEmployeeBlacklistItems.Clear();
        _personalAccessTokens.Clear();
        SaveChangesCallCount = 0;
    }

    /// <summary>
    /// Disposes the fake context by clearing all data.
    /// </summary>
    public void Dispose()
    {
        Clear();
        GC.SuppressFinalize(this);
    }

    #endregion
}
