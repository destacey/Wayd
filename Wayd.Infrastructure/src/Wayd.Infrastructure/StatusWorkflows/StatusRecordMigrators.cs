using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.ProductManagement.Application;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

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
public abstract class StatusRecordMigratorBase<TRecord>(IProductManagementDbContext dbContext)
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
    /// Product Management records carry no scope — every assignment is organization-wide — so
    /// <see cref="Records"/> can only be narrowed by workflow. Honouring a scope means first giving the
    /// record type a way to say which scope it belongs to.
    /// </remarks>
    private const string ScopedRecordsUnsupported =
        "Records of this type have no scope, so they can only be counted or migrated organization-wide.";

    private readonly IProductManagementDbContext _dbContext = dbContext;

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

    /// <summary>The context, for subclasses to expose their own set.</summary>
    protected IProductManagementDbContext DbContext => _dbContext;
}

public sealed class ProductStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Product>(dbContext)
{
    public override string OwnerType => ProductWorkflowOwners.Product.Key;

    protected override IQueryable<Product> Records => DbContext.Products;
}

public sealed class VersionStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Version>(dbContext)
{
    public override string OwnerType => ProductWorkflowOwners.Version.Key;

    protected override IQueryable<Version> Records => DbContext.Versions;
}

public sealed class ReleaseStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Release>(dbContext)
{
    public override string OwnerType => ProductWorkflowOwners.Release.Key;

    protected override IQueryable<Release> Records => DbContext.Releases;
}

public sealed class ReleasePackageStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<ReleasePackage>(dbContext)
{
    public override string OwnerType => ProductWorkflowOwners.ReleasePackage.Key;

    protected override IQueryable<ReleasePackage> Records => DbContext.ReleasePackages;
}

public sealed class DeploymentStatusRecordMigrator(IProductManagementDbContext dbContext)
    : StatusRecordMigratorBase<Deployment>(dbContext)
{
    public override string OwnerType => ProductWorkflowOwners.Deployment.Key;

    protected override IQueryable<Deployment> Records => DbContext.Deployments;
}
