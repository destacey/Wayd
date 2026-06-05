using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.Planning.Application.Persistence;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Models.Iterations;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IPlanningDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all Planning domain tests.
/// </summary>
public class FakePlanningDbContext : IPlanningDbContext, IDisposable
{
    // Planning domain entities
    private readonly List<Iteration> _iterations = [];
    private readonly List<PlanningIntervalObjective> _planningIntervalObjectives = [];
    private readonly List<PlanningInterval> _planningIntervals = [];
    private readonly List<PlanningIntervalIterationSprint> _planningIntervalIterationSprints = [];
    private readonly List<Risk> _risks = [];
    private readonly List<PlanningTeam> _planningTeams = [];
    private readonly List<PlanningIntervalObjectiveHealthCheck> _planningIntervalObjectiveHealthChecks = [];
    private readonly List<Roadmap> _roadmaps = [];
    private readonly List<EstimationScale> _estimationScales = [];
    private readonly List<PokerSession> _pokerSessions = [];

    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];

    // DbSet properties
    public DbSet<Iteration> Iterations => _iterations.AsDbSet();
    public DbSet<PlanningIntervalObjective> PlanningIntervalObjectives => _planningIntervalObjectives.AsDbSet();
    public DbSet<PlanningInterval> PlanningIntervals => _planningIntervals.AsDbSet();
    public DbSet<PlanningIntervalIterationSprint> PlanningIntervalIterationSprints => _planningIntervalIterationSprints.AsDbSet();
    public DbSet<Risk> Risks => _risks.AsDbSet();
    public DbSet<PlanningTeam> PlanningTeams => _planningTeams.AsDbSet();
    public DbSet<PlanningIntervalObjectiveHealthCheck> PlanningIntervalObjectiveHealthChecks => _planningIntervalObjectiveHealthChecks.AsDbSet();
    public DbSet<Roadmap> Roadmaps => _roadmaps.AsDbSet();
    public DbSet<EstimationScale> EstimationScales => _estimationScales.AsDbSet();
    public DbSet<PokerSession> PokerSessions => _pokerSessions.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakePlanningDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Return the total number of entities as a simple success indicator
        var count = _iterations.Count + _planningIntervals.Count + _risks.Count +
                    _planningTeams.Count + _planningIntervalObjectiveHealthChecks.Count + _roadmaps.Count +
                    _estimationScales.Count + _pokerSessions.Count +
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

    // Iteration
    public void AddIteration(Iteration iteration) => _iterations.Add(iteration);
    public void AddIterations(IEnumerable<Iteration> iterations) => _iterations.AddRange(iterations);

    // PlanningInterval
    public void AddPlanningInterval(PlanningInterval planningInterval) => _planningIntervals.Add(planningInterval);
    public void AddPlanningIntervals(IEnumerable<PlanningInterval> planningIntervals) => _planningIntervals.AddRange(planningIntervals);

    // PlanningIntervalObjective
    public void AddPlanningIntervalObjective(PlanningIntervalObjective objective) => _planningIntervalObjectives.Add(objective);
    public void AddPlanningIntervalObjectives(IEnumerable<PlanningIntervalObjective> objectives) => _planningIntervalObjectives.AddRange(objectives);

    // Risk
    public void AddRisk(Risk risk) => _risks.Add(risk);
    public void AddRisks(IEnumerable<Risk> risks) => _risks.AddRange(risks);

    // PlanningTeam
    public void AddPlanningTeam(PlanningTeam planningTeam) => _planningTeams.Add(planningTeam);
    public void AddPlanningTeams(IEnumerable<PlanningTeam> planningTeams) => _planningTeams.AddRange(planningTeams);

    // PlanningIntervalObjectiveHealthCheck
    public void AddPlanningIntervalObjectiveHealthCheck(PlanningIntervalObjectiveHealthCheck healthCheck) => _planningIntervalObjectiveHealthChecks.Add(healthCheck);
    public void AddPlanningIntervalObjectiveHealthChecks(IEnumerable<PlanningIntervalObjectiveHealthCheck> healthChecks) => _planningIntervalObjectiveHealthChecks.AddRange(healthChecks);

    // Roadmap
    public void AddRoadmap(Roadmap roadmap) => _roadmaps.Add(roadmap);
    public void AddRoadmaps(IEnumerable<Roadmap> roadmaps) => _roadmaps.AddRange(roadmaps);

    // EstimationScale
    public void AddEstimationScale(EstimationScale scale) => _estimationScales.Add(scale);
    public void AddEstimationScales(IEnumerable<EstimationScale> scales) => _estimationScales.AddRange(scales);

    // PokerSession
    public void AddPokerSession(PokerSession session) => _pokerSessions.Add(session);
    public void AddPokerSessions(IEnumerable<PokerSession> sessions) => _pokerSessions.AddRange(sessions);

    // Employee
    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddEmployees(IEnumerable<Employee> employees) => _employees.AddRange(employees);

    /// <summary>
    /// Clears all entities from the fake context and resets counters.
    /// Useful for test cleanup or test isolation.
    /// </summary>
    public void Clear()
    {
        _iterations.Clear();
        _planningIntervals.Clear();
        _planningIntervalObjectives.Clear();
        _risks.Clear();
        _planningTeams.Clear();
        _planningIntervalObjectiveHealthChecks.Clear();
        _roadmaps.Clear();
        _estimationScales.Clear();
        _pokerSessions.Clear();
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
