using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Deployments;

/// <summary>
/// Records the release of what a production deployment shipped, where nobody has recorded it yet. The
/// caller saves.
/// </summary>
/// <remarks>
/// A version deployed to production has shipped at that moment, so the deployment is the release's
/// source rather than a second record that could disagree with it. A package's deployment ships the
/// package and every component that changed in it.
/// <para>
/// Only an unreleased record is filled, and through <c>MarkReleased</c>, so the status and its history
/// move exactly as they would had a person recorded it. A released moment already set — entered by hand,
/// or filled by an earlier deployment — is never replaced: a filled one cannot be told from an entered
/// one. A withdrawn record is left alone, and so is one cut after the deployment completed, which the
/// aggregate refuses.
/// </para>
/// </remarks>
internal static class ProductionRelease
{
    public static async Task Record(
        IProductManagementDbContext productManagementDbContext,
        IStatusResolver statusResolver,
        Deployment deployment,
        Instant shippedAt,
        EventActor actor,
        Instant timestamp,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (deployment.EnvironmentCategory != EnvironmentCategory.Production)
        {
            return;
        }

        var versionIds = new List<Guid>();

        if (deployment.VersionId is { } versionId)
        {
            versionIds.Add(versionId);
        }

        if (deployment.PackageId is { } packageId)
        {
            // The manifest must be loaded: MarkReleased refuses an empty one, and an unloaded collection
            // reads as empty.
            var package = await productManagementDbContext.ReleasePackages
                .Include(p => p.Components)
                .FirstOrDefaultAsync(p => p.Id == packageId, cancellationToken);

            if (package is null)
            {
                return;
            }

            await RecordPackage(statusResolver, package, shippedAt, actor, timestamp, logger, cancellationToken);

            // A carried-forward component shipped in an earlier package, so this one did not release it.
            versionIds.AddRange(package.ChangedComponents
                .Where(c => c.VersionId is not null)
                .Select(c => c.VersionId!.Value));
        }

        await RecordVersions(
            productManagementDbContext, statusResolver, versionIds, shippedAt, actor, timestamp, logger, cancellationToken);
    }

    private static async Task RecordPackage(
        IStatusResolver statusResolver,
        ReleasePackage package,
        Instant shippedAt,
        EventActor actor,
        Instant timestamp,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (package.ReleasedAt is not null || package.StatusCategory == StatusCategory.Removed)
        {
            return;
        }

        var status = await statusResolver.ForAlias(
            ProductWorkflowOwners.ReleasePackage.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);

        if (status.IsFailure)
        {
            logger.LogError("Unable to resolve the released package status. Error message: {Error}", status.Error);
            return;
        }

        var result = package.MarkReleased(shippedAt, status.Value, actor, timestamp);
        if (result.IsFailure)
        {
            logger.LogInformation(
                "Release Package {PackageId} was not marked released by its production deployment: {Error}",
                package.Id, result.Error);
        }
    }

    private static async Task RecordVersions(
        IProductManagementDbContext productManagementDbContext,
        IStatusResolver statusResolver,
        List<Guid> versionIds,
        Instant shippedAt,
        EventActor actor,
        Instant timestamp,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (versionIds.Count == 0)
        {
            return;
        }

        var loaded = await productManagementDbContext.Versions
            .Where(v => versionIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        // Checked in memory rather than in the query: an import releases versions row by row before one
        // save, and the database has not seen those yet. The tracked instances have.
        var versions = loaded
            .Where(v => v.ReleasedAt is null && v.StatusCategory != StatusCategory.Removed)
            .ToList();

        if (versions.Count == 0)
        {
            return;
        }

        var status = await statusResolver.ForAlias(
            ProductWorkflowOwners.Version.Key, scopeId: null, (int)ProductStatusAlias.Released, cancellationToken);

        if (status.IsFailure)
        {
            logger.LogError("Unable to resolve the released version status. Error message: {Error}", status.Error);
            return;
        }

        var productIds = versions.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await productManagementDbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        foreach (var version in versions)
        {
            var result = version.MarkReleased(
                shippedAt, status.Value, productNames.GetValueOrDefault(version.ProductId, string.Empty), actor, timestamp);

            if (result.IsFailure)
            {
                logger.LogInformation(
                    "Version {VersionId} was not marked released by its production deployment: {Error}",
                    version.Id, result.Error);
            }
        }
    }
}
