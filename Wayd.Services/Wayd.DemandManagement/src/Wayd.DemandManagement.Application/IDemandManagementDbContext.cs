using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Domain.Models;

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
    /// <summary>Where requests come from: the company's channel vocabulary.</summary>
    DbSet<IntakeChannel> IntakeChannels { get; }

    /// <summary>The review responsibilities requests are submitted to, with their members.</summary>
    DbSet<IntakeDesk> IntakeDesks { get; }

    /// <summary>The asks themselves, with their participants and tags.</summary>
    DbSet<IntakeRequest> IntakeRequests { get; }

    /// <summary>The axes reviewers file requests along, with their tags.</summary>
    DbSet<IntakeRequestTagCategory> IntakeRequestTagCategories { get; }

    /// <summary>
    /// The module's same-Id copies of PPM projects, which outcomes point at. Written only by the replication
    /// handlers.
    /// </summary>
    DbSet<DemandProject> DemandProjects { get; }

    /// <summary>
    /// The shared employee table, read-only: requesters, submitters, desk members and participants are employees.
    /// </summary>
    /// <remarks>
    /// A query rather than a set, so the module can name and check the people its records point at but can
    /// never change an employee. It is a no-tracking query, and EF applies no-tracking to a whole query that
    /// composes one, so never compose it into a query that loads a record a command will change: look the
    /// employee up separately.
    /// </remarks>
    IQueryable<Employee> Employees { get; }

    /// <summary>
    /// The status history of intake requests, read-only, and only theirs.
    /// </summary>
    /// <remarks>
    /// One table holds every owner type's history, so this is narrowed to the intake request owner type before
    /// the module sees it: the module reads when a request arrived and when it entered its status, never another
    /// module's records. History is appended by the aggregate, so nothing here writes it.
    /// </remarks>
    IQueryable<StatusTransition> IntakeRequestStatusTransitions { get; }

    /// <summary>
    /// Saves the tracked changes, together with the events, audit trail and activity log they produce.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
