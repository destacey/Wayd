using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The tables of the <c>Kpis</c> schema: every KPI and its measurements across every subject type, and the
/// organization's categories.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="IDiscussionsDbContext"/>: a module's
/// subject type reads it alongside its own context, and its test fake should not owe the whole global interface.
/// </remarks>
public interface IKpisDbContext
{
    /// <summary>
    /// Every KPI. Reads are keyed by (<c>SubjectType</c>, <c>SubjectId</c>) — <c>SubjectId</c> alone is only unique
    /// within a subject type.
    /// </summary>
    /// <remarks>
    /// Read through this set only after the subject type has said the caller may see the record. Load
    /// <c>Measurements</c> with <c>.Include</c> before changing them: the overlap rule checks the loaded ones only.
    /// </remarks>
    DbSet<Kpi> Kpis { get; }

    /// <summary>
    /// Every measurement, for reading a scorecard's latest values without loading each KPI's history. Change them only
    /// through <see cref="Kpi"/>.
    /// </summary>
    DbSet<KpiMeasurement> KpiMeasurements { get; }

    /// <summary>The organization's KPI categories, one list for every subject type.</summary>
    DbSet<KpiCategory> KpiCategories { get; }

    /// <summary>The shared employee table, for naming who took a measurement.</summary>
    DbSet<Employee> Employees { get; }

    /// <summary>
    /// Writes the KPI's own row on the next save although nothing on it changed, so its row version catches another
    /// save of the same KPI made at the same moment.
    /// </summary>
    /// <remarks>
    /// For a measurement change, which otherwise writes only the measurement's row: two overlapping periods recorded at
    /// once would each pass the overlap rule against the measurements their request loaded.
    /// </remarks>
    void TouchKpi(Kpi kpi);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
