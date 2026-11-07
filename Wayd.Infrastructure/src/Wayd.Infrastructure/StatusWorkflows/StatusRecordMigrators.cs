using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Project = Wayd.ProjectPortfolioManagement.Domain.Models.Project;
using ProjectDeliverable = Wayd.ProjectPortfolioManagement.Domain.Models.ProjectDeliverable;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.Infrastructure.StatusWorkflows;

/// <summary>
/// Moves records of one status-tracked type between workflows.
/// </summary>
/// <remarks>
/// These live in Infrastructure because it is the only project referencing both the engine's contracts
/// in Common.Application and the modules' DbContexts. Common cannot see a Product, and Product
/// Management should not own a loop that every other adopting module would then copy.
/// <para>
/// Not marked as services — see <see cref="IStatusRecordMigrator"/> for why, and register explicitly.
/// </para>
/// </remarks>
public abstract class StatusRecordMigratorBase<TRecord>
    : IStatusRecordMigrator, IStatusRecordCounter
    where TRecord : StatusTrackedEntity
{
    /// <summary>
    /// How many records are loaded at once.
    /// </summary>
    /// <remarks>
    /// Bounded so a large catalogue does not materialize in one go. The caller saves once at the end,
    /// so this bounds memory rather than transaction size.
    /// </remarks>
    private const int BatchSize = 500;

    /// <summary>
    /// The refusal for a scoped migration or count.
    /// </summary>
    /// <remarks>
    /// Product Management records and project deliverables carry no scope — every assignment is
    /// organization-wide — so <see cref="Records"/> can only be narrowed by workflow. Honouring a scope means
    /// first giving the record type a way to say which scope it belongs to.
    /// </remarks>
    private const string ScopedRecordsUnsupported =
        "Records of this type have no scope, so they can only be counted or migrated organization-wide.";

    /// <inheritdoc/>
    public abstract string OwnerType { get; }

    /// <summary>The records this migrator owns.</summary>
    protected abstract IQueryable<TRecord> Records { get; }

    /// <inheritdoc/>
    public async Task<Result<int>> Migrate(
        StatusRemap remap,
        Guid? scopeId,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(remap);

        if (scopeId is not null)
        {
            return Result.Failure<int>(ScopedRecordsUnsupported);
        }

        var allowed = await CheckRemap(remap, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<int>(allowed.Error);
        }

        var migrated = 0;
        var offset = 0;

        while (true)
        {
            // Paged rather than filtered-and-drained: the records stay in the change tracker unsaved,
            // so a re-query would return the same rows forever. Ordering by Id keeps the window stable.
            var batch = await Records
                .Where(r => r.StatusWorkflowId == remap.FromWorkflowId)
                .OrderBy(r => r.Id)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var record in batch)
            {
                var result = record.SwitchWorkflow(remap, actor, timestamp);
                if (result.IsFailure)
                {
                    // One unmappable record means the remap does not describe this data. Stopping
                    // leaves nothing saved, because the caller owns the save.
                    return Result.Failure<int>(result.Error);
                }

                migrated++;
            }

            offset += batch.Count;
        }

        return Result.Success(migrated);
    }

    /// <summary>
    /// Refuses a remap that would leave records breaking a rule of their owner, before any record moves. The
    /// failure is what the administrator running the migration sees, so it says what to map differently.
    /// </summary>
    protected virtual Task<Result> CheckRemap(StatusRemap remap, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId,
        Guid? scopeId,
        CancellationToken cancellationToken)
    {
        if (scopeId is not null)
        {
            return Result.Failure<IReadOnlyDictionary<Guid, int>>(ScopedRecordsUnsupported);
        }

        var counts = await Records
            .Where(r => r.StatusWorkflowId == workflowId)
            .GroupBy(r => r.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyDictionary<Guid, int>>(counts.ToDictionary(c => c.StatusId, c => c.Count));
    }

    /// <inheritdoc/>
    public Task<Result<IReadOnlyList<Guid>>> WorkflowsBesides(
        Guid workflowId,
        Guid scopeId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<IReadOnlyList<Guid>>(ScopedRecordsUnsupported));
}

public sealed class ProductStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Product>
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public override string OwnerType => ProductWorkflowOwners.Product.Key;

    protected override IQueryable<Product> Records => _dbContext.Products;
}

public sealed class VersionStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Version>
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public override string OwnerType => ProductWorkflowOwners.Version.Key;

    protected override IQueryable<Version> Records => _dbContext.Versions;
}

public sealed class ReleaseStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Release>
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public override string OwnerType => ProductWorkflowOwners.Release.Key;

    protected override IQueryable<Release> Records => _dbContext.Releases;
}

public sealed class ReleasePackageStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<ReleasePackage>
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public override string OwnerType => ProductWorkflowOwners.ReleasePackage.Key;

    protected override IQueryable<ReleasePackage> Records => _dbContext.ReleasePackages;
}

public sealed class DeploymentStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Deployment>
{
    private readonly IProductManagementDbContext _dbContext = dbContext;

    public override string OwnerType => ProductWorkflowOwners.Deployment.Key;

    protected override IQueryable<Deployment> Records => _dbContext.Deployments;
}

/// <summary>
/// Moves project deliverables between workflows.
/// </summary>
/// <remarks>
/// The one change that reaches a deliverable without going through its <see cref="Project"/>: a remap
/// translates statuses onto another workflow, which is the engine's doing rather than a change to the
/// project's scope, so it needs no project leadership and is allowed on a closed project — but never into an
/// open status there, since a completed or canceled project has nothing left to deliver.
/// <para>
/// It does not advance the project's revision. A change prepared before the switch names a status of the old
/// workflow, which the deliverable commands already refuse.
/// </para>
/// </remarks>
public sealed class ProjectDeliverableStatusRecordMigrator(IProjectPortfolioManagementDbContext dbContext)
    : StatusRecordMigratorBase<ProjectDeliverable>
{
    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;

    public override string OwnerType => PpmWorkflowOwners.ProjectDeliverable.Key;

    protected override IQueryable<ProjectDeliverable> Records => _dbContext.ProjectDeliverables;

    /// <inheritdoc/>
    protected override async Task<Result> CheckRemap(StatusRemap remap, CancellationToken cancellationToken)
    {
        var closedProjects = _dbContext.Projects
            .Where(p => p.Status == ProjectStatus.Completed || p.Status == ProjectStatus.Canceled);

        var held = await _dbContext.ProjectDeliverables
            .Where(d => d.StatusWorkflowId == remap.FromWorkflowId && closedProjects.Any(p => p.Id == d.ProjectId))
            .GroupBy(d => new { d.StatusId, d.StatusName })
            .Select(g => new { g.Key.StatusId, g.Key.StatusName, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var reopened = held
            .Select(h => (h.StatusName, h.Count, Target: remap.For(h.StatusId)))
            .Where(h => h.Target?.Category is StatusCategory.Proposed or StatusCategory.Active)
            .OrderBy(h => h.StatusName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (reopened.Count == 0)
        {
            return Result.Success();
        }

        var moves = string.Join("; ", reopened.Select(h =>
            $"{h.Count} in '{h.StatusName}' would move to '{h.Target!.Name}'"));

        return Result.Failure(
            "Deliverables of completed or canceled projects must stay closed, but this mapping would reopen them: " +
            $"{moves}. Map those statuses to a Done or Removed status instead.");
    }
}
