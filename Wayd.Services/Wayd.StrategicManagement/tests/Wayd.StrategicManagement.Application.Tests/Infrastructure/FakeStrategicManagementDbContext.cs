using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.StrategicManagement.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.StrategicManagement.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IStrategicManagementDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all StrategicManagement domain tests.
/// </summary>
public class FakeStrategicManagementDbContext : IStrategicManagementDbContext, IDisposable
{
    // StrategicManagement domain entities
    private readonly List<Strategy> _strategies = [];
    private readonly List<StrategicTheme> _strategicThemes = [];
    private readonly List<Vision> _visions = [];

    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];

    // DbSet properties
    public DbSet<Strategy> Strategies => _strategies.AsDbSet();
    public DbSet<StrategicTheme> StrategicThemes => _strategicThemes.AsDbSet();
    public DbSet<Vision> Visions => _visions.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakeStrategicManagementDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Return the total number of entities as a simple success indicator
        var count = _strategies.Count + _strategicThemes.Count + _visions.Count +
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

    // Strategy
    public void AddStrategy(Strategy strategy) => _strategies.Add(strategy);
    public void AddStrategies(IEnumerable<Strategy> strategies) => _strategies.AddRange(strategies);

    // StrategicTheme
    public void AddStrategicTheme(StrategicTheme theme) => _strategicThemes.Add(theme);
    public void AddStrategicThemes(IEnumerable<StrategicTheme> themes) => _strategicThemes.AddRange(themes);

    // Vision
    public void AddVision(Vision vision) => _visions.Add(vision);
    public void AddVisions(IEnumerable<Vision> visions) => _visions.AddRange(visions);

    // Employee
    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddEmployees(IEnumerable<Employee> employees) => _employees.AddRange(employees);

    /// <summary>
    /// Clears all entities from the fake context and resets counters.
    /// Useful for test cleanup or test isolation.
    /// </summary>
    public void Clear()
    {
        _strategies.Clear();
        _strategicThemes.Clear();
        _visions.Clear();
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
