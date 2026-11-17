using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Tests.Shared.Infrastructure;

/// <summary>
/// An in-memory <see cref="IKpisDbContext"/> for KPI handlers and the modules' subject types.
/// </summary>
/// <remarks>
/// <see cref="KpiMeasurements"/> is read from the KPIs' own measurements, as the real context's table would be, so a
/// test adds measurements through the aggregate only.
/// </remarks>
public sealed class FakeKpisDbContext : IKpisDbContext
{
    private readonly List<Kpi> _kpis = [];
    private readonly List<KpiCategory> _categories = [];
    private readonly List<Employee> _employees = [];

    public DbSet<Kpi> Kpis => _kpis.AsDbSet();
    public DbSet<KpiMeasurement> KpiMeasurements => _kpis.SelectMany(k => k.Measurements).ToList().AsDbSet();
    public DbSet<KpiCategory> KpiCategories => _categories.AsDbSet();
    public DbSet<Employee> Employees => _employees.AsDbSet();

    /// <summary>The number of times <see cref="SaveChangesAsync"/> has been called.</summary>
    public int SaveChangesCallCount { get; private set; }

    /// <summary>When set, the next save throws it, as a collision with another change would.</summary>
    public Exception? NextSaveThrows { get; set; }

    /// <summary>The KPIs <see cref="TouchKpi"/> was called for, in order.</summary>
    public List<Kpi> TouchedKpis { get; } = [];

    public void TouchKpi(Kpi kpi) => TouchedKpis.Add(kpi);

    public void AddKpis(params Kpi[] kpis) => _kpis.AddRange(kpis);

    public void AddCategories(params KpiCategory[] categories) => _categories.AddRange(categories);

    public void AddEmployees(params Employee[] employees) => _employees.AddRange(employees);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;

        if (NextSaveThrows is { } exception)
        {
            NextSaveThrows = null;
            throw exception;
        }

        return Task.FromResult(0);
    }
}
