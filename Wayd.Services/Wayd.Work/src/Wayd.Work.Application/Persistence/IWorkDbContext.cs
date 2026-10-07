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
    IQueryable<WorkItem> SearchWorkItems(string searchTerm, int top);

    /// <summary>Opens a transaction over several saves; see <see cref="IUnitOfWork"/>.</summary>
    Task<IUnitOfWork> BeginUnitOfWork(CancellationToken cancellationToken);
}
