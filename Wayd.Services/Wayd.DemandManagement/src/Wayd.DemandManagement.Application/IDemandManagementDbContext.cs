namespace Wayd.DemandManagement.Application;

/// <summary>
/// Everything the Demand Management module can reach in the database: its own tables, and nothing else.
/// </summary>
/// <remarks>
/// Deliberately not an <c>IWaydDbContext</c>. That interface hands a module the database facade, the
/// change tracker and the shared identity tables, so a handler could read or write anything; this one makes
/// the module's boundary something the compiler enforces rather than a convention.
/// <c>DemandManagementBoundaryTests</c> fails the build if the module reaches past it.
/// <para>
/// It is still a view over the one shared <c>WaydDbContext</c>, so a save commits these rows together with
/// their activity log, audit trail and outbox envelopes exactly as every other module's does. Data owned by
/// another module arrives here as a same-Id copy fed by that module's events, or as a narrow read-only set
/// added to this interface when a handler needs it.
/// </para>
/// </remarks>
public interface IDemandManagementDbContext
{
    /// <summary>
    /// Saves the tracked changes, together with the events, audit trail and activity log they produce.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
