using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.Persistence;

public interface IWorkDbContext : IWaydDbContext
{
    DbSet<Iteration> Iterations { get; }
    DbSet<WorkTypeHierarchy> WorkTypeHierarchies { get; }
    DbSet<Workflow> Workflows { get; }
    DbSet<WorkItemReference> WorkItemReferences { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<WorkItemDependency> WorkItemDependencies { get; }
    DbSet<WorkItemHierarchy> WorkItemHierarchies { get; }
    DbSet<WorkItemStateHistory> WorkItemStateHistory { get; }
    DbSet<WorkItemSourceRevision> WorkItemSourceRevisions { get; }
    DbSet<WorkItemRevisionFill> WorkItemRevisionFills { get; }
    DbSet<WorkProcess> WorkProcesses { get; }
    DbSet<WorkProject> WorkProjects { get; }
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkStatus> WorkStatuses { get; }
    DbSet<WorkTeam> WorkTeams { get; }
    DbSet<WorkType> WorkTypes { get; }

    /// <summary>
    /// Returns work items matching the search term using full-text search when available,
    /// ordered by key prefix then key number, limited to <paramref name="top"/> results.
    /// </summary>
    /// <param name="searchTerm">Matched against the title, the key and the parent's key.</param>
    /// <param name="tier">When set, only work items whose type is in this tier; applied before the limit.</param>
    /// <param name="top">The most work items to return.</param>
    IQueryable<WorkItem> SearchWorkItems(string searchTerm, WorkTypeTier? tier, int top);

    /// <summary>Opens a transaction over several saves; see <see cref="IUnitOfWork"/>.</summary>
    Task<IUnitOfWork> BeginUnitOfWork(CancellationToken cancellationToken);

    /// <summary>
    /// Raises the command timeout for an operation that legitimately runs long, putting back the context's own
    /// ceiling when disposed.
    /// </summary>
    IDisposable WithCommandTimeout(TimeSpan timeout);
}
