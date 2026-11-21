namespace Wayd.Common.Application.Kpis;

/// <summary>
/// Changes KPIs on behalf of a module rather than a caller: for now, removing every KPI on a record the module is
/// deleting.
/// </summary>
/// <remarks>
/// <para>
/// <strong>It checks no caller.</strong> The module deleting its record has already decided the caller may; the KPIs go
/// because the record goes.
/// </para>
/// <para>
/// <strong>It never saves.</strong> The removals are tracked on the shared context, so the caller's save commits them
/// with the deleted record, in one transaction.
/// </para>
/// </remarks>
public interface IKpiWriter
{
    /// <summary>
    /// Removes every KPI a record has, open or closed, with their measurements and checkpoint plans. A KPI has no
    /// foreign key to its subject, so it would otherwise outlive the record. Each removal raises its own event.
    /// </summary>
    /// <exception cref="ArgumentException">The subject type is not registered.</exception>
    Task RemoveAll(string subjectType, Guid subjectId, EventActor actor, CancellationToken cancellationToken);
}
