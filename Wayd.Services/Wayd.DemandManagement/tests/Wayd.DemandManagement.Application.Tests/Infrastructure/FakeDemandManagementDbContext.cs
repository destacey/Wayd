using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Infrastructure;

/// <summary>
/// An in-memory <see cref="IDemandManagementDbContext"/> for handler tests.
/// </summary>
/// <remarks>
/// Queries run as LINQ to objects, so a test here proves a handler's rules, not that EF can translate them;
/// the Testcontainers tests in <c>Wayd.Infrastructure.IntegrationTests</c> cover translation.
/// </remarks>
public sealed class FakeDemandManagementDbContext : IDemandManagementDbContext
{
    private readonly List<IntakeChannel> _channels = [];
    private readonly List<IntakeDesk> _desks = [];
    private readonly List<IntakeRequest> _requests = [];
    private readonly List<IntakeRequestTagCategory> _tagCategories = [];
    private readonly List<Employee> _employees = [];
    private readonly List<StatusTransition> _transitions = [];
    private readonly List<DemandProject> _projects = [];

    public DbSet<IntakeChannel> IntakeChannels => _channels.AsDbSet();
    public DbSet<DemandProject> DemandProjects => _projects.AsDbSet();
    public DbSet<IntakeDesk> IntakeDesks => _desks.AsDbSet();
    public DbSet<IntakeRequest> IntakeRequests => _requests.AsDbSet();
    public DbSet<IntakeRequestTagCategory> IntakeRequestTagCategories => _tagCategories.AsDbSet();
    public IQueryable<Employee> Employees => _employees.AsDbSet();
    public IQueryable<StatusTransition> IntakeRequestStatusTransitions => _transitions.AsDbSet();

    /// <summary>
    /// The number of times <see cref="SaveChangesAsync"/> has been called.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Makes the next save fail as the database would when a row it writes breaks a constraint or was changed
    /// underneath it, which an in-memory save can never do on its own.
    /// </summary>
    public DbUpdateException? FailNextSaveWith { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        if (FailNextSaveWith is { } failure)
        {
            FailNextSaveWith = null;
            throw failure;
        }

        return Task.FromResult(0);
    }

    public void AddChannels(params IntakeChannel[] channels) => _channels.AddRange(channels);
    public void AddDesks(params IntakeDesk[] desks) => _desks.AddRange(desks);
    public void AddEmployees(params Employee[] employees) => _employees.AddRange(employees);
    public void AddTagCategories(params IntakeRequestTagCategory[] categories) => _tagCategories.AddRange(categories);
    public void AddProjects(params DemandProject[] projects) => _projects.AddRange(projects);

    /// <summary>
    /// Adds requests together with the status history they have recorded so far, as a save would persist it.
    /// </summary>
    public void AddRequests(params IntakeRequest[] requests)
    {
        _requests.AddRange(requests);
        foreach (var request in requests)
            _transitions.AddRange(request.DrainStatusTransitions());
    }

    /// <summary>Adds history entries directly, for requests a faker built without recording any.</summary>
    public void AddStatusTransitions(params StatusTransition[] transitions) => _transitions.AddRange(transitions);
}
