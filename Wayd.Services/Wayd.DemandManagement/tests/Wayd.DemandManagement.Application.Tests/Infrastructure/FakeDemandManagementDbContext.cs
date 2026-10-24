using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
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
    private readonly List<Employee> _employees = [];

    public DbSet<IntakeChannel> IntakeChannels => _channels.AsDbSet();
    public DbSet<IntakeDesk> IntakeDesks => _desks.AsDbSet();
    public DbSet<IntakeRequest> IntakeRequests => _requests.AsDbSet();
    public IQueryable<Employee> Employees => _employees.AsDbSet();

    /// <summary>
    /// The number of times <see cref="SaveChangesAsync"/> has been called.
    /// </summary>
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }

    public void AddChannels(params IntakeChannel[] channels) => _channels.AddRange(channels);
    public void AddDesks(params IntakeDesk[] desks) => _desks.AddRange(desks);
    public void AddRequests(params IntakeRequest[] requests) => _requests.AddRange(requests);
    public void AddEmployees(params Employee[] employees) => _employees.AddRange(employees);
}
