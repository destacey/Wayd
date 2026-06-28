using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Scoring;
using Wayd.Tests.Shared.Infrastructure;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.Tests.Infrastructure;

/// <summary>
/// A test double for IWorkDbContext that provides in-memory collections for all DbSets.
/// This eliminates the need for complex Moq setups in tests and is reusable across all Work domain tests.
/// </summary>
public class FakeWorkDbContext : IWorkDbContext, IDisposable
{
    // Work domain entities
    private readonly List<Workspace> _workspaces = [];
    private readonly List<WorkProcess> _workProcesses = [];
    private readonly List<WorkItem> _workItems = [];
    private readonly List<WorkType> _workTypes = [];
    private readonly List<WorkStatus> _workStatuses = [];
    private readonly List<Workflow> _workflows = [];
    private readonly List<WorkTypeHierarchy> _workTypeHierarchies = [];
    private readonly List<WorkTeam> _workTeams = [];
    private readonly List<WorkProject> _workProjects = [];
    private readonly List<WorkIteration> _workIterations = [];
    private readonly List<WorkItemReference> _workItemReferences = [];
    private readonly List<WorkItemHierarchy> _workItemHierarchies = [];
    private readonly List<WorkItemDependency> _workItemDependencies = [];

    // Common domain entities
    private readonly List<Employee> _employees = [];
    private readonly List<ExternalEmployeeBlacklistItem> _externalEmployeeBlacklistItems = [];
    private readonly List<OidcProvider> _oidcProviders = [];
    private readonly List<PersonalAccessToken> _personalAccessTokens = [];
    private readonly List<User> _waydUsers = [];
    private readonly List<ScoringModel> _scoringModels = [];

    // DbSet properties
    public DbSet<Workspace> Workspaces => _workspaces.AsDbSet();
    public DbSet<WorkProcess> WorkProcesses => _workProcesses.AsDbSet();
    public DbSet<WorkItem> WorkItems => _workItems.AsDbSet();
    public DbSet<WorkType> WorkTypes => _workTypes.AsDbSet();
    public DbSet<WorkStatus> WorkStatuses => _workStatuses.AsDbSet();
    public DbSet<Workflow> Workflows => _workflows.AsDbSet();
    public DbSet<WorkTypeHierarchy> WorkTypeHierarchies => _workTypeHierarchies.AsDbSet();
    public DbSet<WorkTeam> WorkTeams => _workTeams.AsDbSet();
    public DbSet<WorkProject> WorkProjects => _workProjects.AsDbSet();
    public DbSet<WorkIteration> WorkIterations => _workIterations.AsDbSet();
    public DbSet<WorkItemReference> WorkItemReferences => _workItemReferences.AsDbSet();
    public DbSet<WorkItemHierarchy> WorkItemHierarchies => _workItemHierarchies.AsDbSet();
    public DbSet<WorkItemDependency> WorkItemDependencies => _workItemDependencies.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();
    public DbSet<ExternalEmployeeBlacklistItem> ExternalEmployeeBlacklistItems => _externalEmployeeBlacklistItems.AsDbSet();
    public DbSet<OidcProvider> OidcProviders => _oidcProviders.AsDbSet();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => _personalAccessTokens.AsDbSet();
    public DbSet<User> WaydUsers => _waydUsers.AsDbSet();
    public DbSet<ScoringModel> ScoringModels => _scoringModels.AsDbSet();

    // ChangeTracker - we can't create a real one, so we return null and the handler uses defensive coding (_workDbContext.ChangeTracker?.Clear())
    public ChangeTracker ChangeTracker => null!;

    public DatabaseFacade Database => throw new NotImplementedException("Database operations are not supported in FakeWorkDbContext. Use integration tests with a real DbContext for database-specific functionality.");

    public IQueryable<WorkItem> SearchWorkItems(string searchTerm, int top) =>
        new TestAsyncEnumerable<WorkItem>(
            _workItems
                .Where(e => e.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || ((string)e.Key).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                    || (e.ParentId.HasValue && ((string)e.Parent!.Key).Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                .Take(top));

    /// <summary>
    /// Gets the number of times SaveChangesAsync has been called.
    /// Useful for asserting that the handler saved changes at the expected times.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        // Return the total number of entities as a simple success indicator
        var count = _workspaces.Count + _workProcesses.Count + _workItems.Count +
                    _workTypes.Count + _workStatuses.Count + _workflows.Count +
                    _workTypeHierarchies.Count + _workTeams.Count + _workProjects.Count +
                    _workIterations.Count + _employees.Count + _externalEmployeeBlacklistItems.Count +
                    _personalAccessTokens.Count;
        return Task.FromResult(count);
    }

    public EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class
    {
        // For unit tests, entity tracking is typically not needed
        // If you need this for specific tests, consider using integration tests with a real DbContext
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    public EntityEntry Entry(object entity)
    {
        // For unit tests, entity tracking is typically not needed
        throw new NotImplementedException("Entry tracking is not needed for unit tests. If you need this, consider using integration tests with a real DbContext.");
    }

    #region Helper Methods for Test Setup

    // Workspace
    public void AddWorkspace(Workspace workspace) => _workspaces.Add(workspace);
    public void AddWorkspaces(IEnumerable<Workspace> workspaces) => _workspaces.AddRange(workspaces);

    // WorkProcess
    public void AddWorkProcess(WorkProcess workProcess) => _workProcesses.Add(workProcess);
    public void AddWorkProcesses(IEnumerable<WorkProcess> workProcesses) => _workProcesses.AddRange(workProcesses);

    // WorkItem
    public void AddWorkItem(WorkItem workItem) => _workItems.Add(workItem);
    public void AddWorkItems(IEnumerable<WorkItem> workItems) => _workItems.AddRange(workItems);

    // WorkType
    public void AddWorkType(WorkType workType) => _workTypes.Add(workType);
    public void AddWorkTypes(IEnumerable<WorkType> workTypes) => _workTypes.AddRange(workTypes);

    // WorkStatus
    public void AddWorkStatus(WorkStatus workStatus) => _workStatuses.Add(workStatus);
    public void AddWorkStatuses(IEnumerable<WorkStatus> workStatuses) => _workStatuses.AddRange(workStatuses);

    // Workflow
    public void AddWorkflow(Workflow workflow) => _workflows.Add(workflow);
    public void AddWorkflows(IEnumerable<Workflow> workflows) => _workflows.AddRange(workflows);

    // WorkTypeHierarchy
    public void AddWorkTypeHierarchy(WorkTypeHierarchy hierarchy) => _workTypeHierarchies.Add(hierarchy);
    public void AddWorkTypeHierarchies(IEnumerable<WorkTypeHierarchy> hierarchies) => _workTypeHierarchies.AddRange(hierarchies);

    // WorkTeam
    public void AddWorkTeam(WorkTeam team) => _workTeams.Add(team);
    public void AddWorkTeams(IEnumerable<WorkTeam> teams) => _workTeams.AddRange(teams);

    // WorkProject
    public void AddWorkProject(WorkProject project) => _workProjects.Add(project);
    public void AddWorkProjects(IEnumerable<WorkProject> projects) => _workProjects.AddRange(projects);

    // WorkIteration
    public void AddWorkIteration(WorkIteration iteration) => _workIterations.Add(iteration);
    public void AddWorkIterations(IEnumerable<WorkIteration> iterations) => _workIterations.AddRange(iterations);

    // Employee
    public void AddEmployee(Employee employee) => _employees.Add(employee);
    public void AddEmployees(IEnumerable<Employee> employees) => _employees.AddRange(employees);

    /// <summary>
    /// Clears all entities from the fake context and resets counters.
    /// Useful for test cleanup or test isolation.
    /// </summary>
    public void Clear()
    {
        _workspaces.Clear();
        _workProcesses.Clear();
        _workItems.Clear();
        _workTypes.Clear();
        _workStatuses.Clear();
        _workflows.Clear();
        _workTypeHierarchies.Clear();
        _workTeams.Clear();
        _workProjects.Clear();
        _workIterations.Clear();
        _workItemReferences.Clear();
        _workItemHierarchies.Clear();
        _workItemDependencies.Clear();
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
