using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Models;
using Wayd.Organization.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Organization.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IOrganizationDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all Organization domain tests.
/// </summary>
public class FakeOrganizationDbContext : IOrganizationDbContext, IDisposable
{
    // Organization domain entities
    private readonly List<BaseTeam> _baseTeams = [];
    private readonly List<Team> _teams = [];
    private readonly List<TeamOfTeams> _teamOfTeams = [];
    private readonly List<TeamOperatingModel> _teamOperatingModels = [];
    private readonly List<TeamMemberRole> _teamMemberRoles = [];
    private readonly List<TeamMember> _teamMembers = [];

    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];

    // DbSet properties
    public DbSet<BaseTeam> BaseTeams => _baseTeams.AsDbSet();
    public DbSet<Team> Teams => _teams.AsDbSet();
    public DbSet<TeamOfTeams> TeamOfTeams => _teamOfTeams.AsDbSet();
    public DbSet<TeamOperatingModel> TeamOperatingModels => _teamOperatingModels.AsDbSet();
    public DbSet<TeamMemberRole> TeamMemberRoles => _teamMemberRoles.AsDbSet();
    public DbSet<TeamMember> TeamMembers => _teamMembers.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakeOrganizationDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Return the total number of entities as a simple success indicator
        var count = _baseTeams.Count + _teams.Count + _teamOfTeams.Count + _teamOperatingModels.Count +
                    _teamMemberRoles.Count + _teamMembers.Count +
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

    #region Graph Table Sync Methods

    /// <summary>
    /// Gets the number of times <see cref="UpsertTeamNode"/> has been called. Useful for asserting that a
    /// handler synced each new team into the graph tables exactly once.
    /// </summary>
    public int UpsertTeamNodeCallCount { get; private set; }

    /// <summary>
    /// Records the call as a no-op. The real graph sync requires SQL graph tables, but handlers legitimately
    /// call this after a relational save, so the fake counts calls rather than throwing.
    /// </summary>
    public Task<int> UpsertTeamNode(TeamNode teamNode, CancellationToken cancellationToken)
    {
        UpsertTeamNodeCallCount++;
        return Task.FromResult(1);
    }

    /// <summary>
    /// Gets the number of times <see cref="UpsertTeamMembershipEdge"/> has been called. Useful for asserting
    /// that a handler synced each new membership edge into the graph tables exactly once.
    /// </summary>
    public int UpsertTeamMembershipEdgeCallCount { get; private set; }

    /// <summary>
    /// Records the call as a no-op. The real graph sync requires SQL graph tables, but handlers legitimately
    /// call this after a relational save, so the fake counts calls rather than throwing.
    /// </summary>
    public Task<int> UpsertTeamMembershipEdge(TeamMembershipEdge teamMembershipEdge, CancellationToken cancellationToken)
    {
        UpsertTeamMembershipEdgeCallCount++;
        return Task.FromResult(1);
    }

    /// <summary>
    /// Graph table sync operations are not supported in the fake context.
    /// These require actual database operations with SQL graph tables.
    /// Use integration tests with a real DbContext for testing graph functionality.
    /// </summary>
    public Task<int> DeleteTeamMembershipEdge(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Graph table sync operations are not supported in FakeOrganizationDbContext. Use integration tests with a real DbContext for graph-specific functionality.");
    }

    #endregion

    #region Helper Methods for Test Setup

    // BaseTeam
    public void AddBaseTeam(BaseTeam baseTeam) => _baseTeams.Add(baseTeam);
    public void AddBaseTeams(IEnumerable<BaseTeam> baseTeams) => _baseTeams.AddRange(baseTeams);

    // Team
    public void AddTeam(Team team)
    {
        _teams.Add(team);
        _baseTeams.Add(team);
    }

    public void AddTeams(IEnumerable<Team> teams)
    {
        _teams.AddRange(teams);
        _baseTeams.AddRange(teams);
    }

    // TeamOfTeams
    public void AddTeamOfTeams(TeamOfTeams teamOfTeams)
    {
        _teamOfTeams.Add(teamOfTeams);
        _baseTeams.Add(teamOfTeams);
    }

    public void AddTeamOfTeams(IEnumerable<TeamOfTeams> teamOfTeams)
    {
        _teamOfTeams.AddRange(teamOfTeams);
        _baseTeams.AddRange(teamOfTeams);
    }

    // TeamOperatingModel
    public void AddTeamOperatingModel(TeamOperatingModel operatingModel) => _teamOperatingModels.Add(operatingModel);
    public void AddTeamOperatingModels(IEnumerable<TeamOperatingModel> operatingModels) => _teamOperatingModels.AddRange(operatingModels);

    // TeamMemberRole
    public void AddTeamMemberRole(TeamMemberRole role) => _teamMemberRoles.Add(role);
    public void AddTeamMemberRoles(IEnumerable<TeamMemberRole> roles) => _teamMemberRoles.AddRange(roles);

    // TeamMember
    public void AddTeamMember(TeamMember member) => _teamMembers.Add(member);
    public void AddTeamMembers(IEnumerable<TeamMember> members) => _teamMembers.AddRange(members);

    // Employee
    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddEmployees(IEnumerable<Employee> employees) => _employees.AddRange(employees);

    /// <summary>
    /// Clears all entities from the fake context and resets counters.
    /// Useful for test cleanup or test isolation.
    /// </summary>
    public void Clear()
    {
        _baseTeams.Clear();
        _teams.Clear();
        _teamOfTeams.Clear();
        _teamOperatingModels.Clear();
        _teamMemberRoles.Clear();
        _teamMembers.Clear();
        _employees.Clear();
        _externalEmployeeBlacklistItems.Clear();
        _personalAccessTokens.Clear();
        SaveChangesCallCount = 0;
        UpsertTeamNodeCallCount = 0;
        UpsertTeamMembershipEdgeCallCount = 0;
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
