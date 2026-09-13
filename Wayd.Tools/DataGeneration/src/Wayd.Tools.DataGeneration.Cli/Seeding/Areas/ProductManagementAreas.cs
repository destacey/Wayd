using Wayd.Tools.DataGeneration.Cli.Csv;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

/// <summary>The stable names the Product Management areas are known by, so dependencies read as references.</summary>
public static class ProductManagementArea
{
    public const string FeatureFlag = "product-management.feature-flag";
    public const string Environments = "product-management.deployment-environments";
    public const string Products = "product-management.products";
    public const string Versions = "product-management.versions";
    public const string ReleasePackages = "product-management.release-packages";
    public const string Releases = "product-management.releases";
    public const string Deployments = "product-management.deployments";

    /// <summary>The feature flag every Product Management endpoint is gated on.</summary>
    public const string FeatureFlagName = "product-management";
}

/// <summary>A Product Management area, which has nothing to do when the seed was asked to skip it.</summary>
public abstract class ProductManagementSeedArea(string name, params string[] dependsOn) : SeedArea(name, dependsOn)
{
    protected GeneratedProductManagement Data(SeedContext context) =>
        context.ProductManagement ?? throw new SeedException($"Area '{Name}' ran without a generated Product Management dataset.");
}

/// <summary>
/// Switches the module on. Every Product Management endpoint answers 404 while its flag is off, and the
/// flag is seeded off.
/// </summary>
public sealed class ProductManagementFeatureFlagArea() : ProductManagementSeedArea(ProductManagementArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement is not null;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var enabled = await context.Client.EnsureFeatureFlagEnabled(ProductManagementArea.FeatureFlagName, cancellationToken);

        context.Log(enabled
            ? $"Enabled the '{ProductManagementArea.FeatureFlagName}' feature flag."
            : $"The '{ProductManagementArea.FeatureFlagName}' feature flag is already enabled.");
    }
}

/// <summary>
/// Loads the environments. Deployments name theirs, so the retired ones a backfill points at have to be
/// here too.
/// </summary>
public sealed class DeploymentEnvironmentsArea() : ProductManagementSeedArea(
    ProductManagementArea.Environments, ProductManagementArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.Environments.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var environments = Data(context).Environments;
        context.Log($"Importing {environments.Count} deployment environments...");

        var rows = environments.Select(e => new DeploymentEnvironmentCsvRow
        {
            ImportId = e.Name,
            Name = e.Name,
            Category = e.Category,
            RingOrder = e.RingOrder,
            IsActive = e.IsActive,
        });

        var run = await context.Client.ImportDeploymentEnvironments(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>
/// Loads the catalog in one file. A product's parent has to be a row in the same file, so the whole tree
/// travels together.
/// </summary>
public sealed class ProductsArea() : ProductManagementSeedArea(
    ProductManagementArea.Products, ProductManagementArea.FeatureFlag)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.Products.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var products = Data(context).Products;
        context.Log($"Importing {products.Count} products...");

        var rows = products.Select(p => new ProductCsvRow
        {
            ImportId = p.Name,
            Name = p.Name,
            Description = p.Description,
            ProductTypeName = p.ProductTypeName,
            ParentImportId = p.ParentName,
            Status = p.Status,
            Tags = p.Tags,
        });

        var run = await context.Client.ImportProducts(CsvFile.ToBytes(rows), cancellationToken);
        context.Publish(Name, run.CreatedIdsByImportId);
    }
}

/// <summary>Loads every version, resolving its product to the id the catalog run created.</summary>
public sealed class VersionsArea() : ProductManagementSeedArea(
    ProductManagementArea.Versions, ProductManagementArea.Products)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.Versions.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var versions = Data(context).Versions;

        var rows = versions.Select(v => new VersionCsvRow
        {
            ImportId = v.Handle,
            ProductId = context.Id(ProductManagementArea.Products, v.ProductName),
            Number = v.Number,
            TargetDate = v.TargetDate,
            CutDate = v.CutDate,
            ReleasedDate = v.ReleasedDate,
            Notes = v.Notes,
        }).ToList();

        // Rows are independent, so the product grouping only keeps one product's history in one file.
        var batches = Batch(rows, r => r.ProductId);
        context.Log($"Importing {versions.Count} versions in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "versions", batches,
            batch => context.Client.ImportVersions(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}

/// <summary>
/// Loads the release packages with their manifests. Runs after the versions, because a manifest line links
/// to a version record only if one already exists for that product and number.
/// </summary>
public sealed class ReleasePackagesArea() : ProductManagementSeedArea(
    ProductManagementArea.ReleasePackages, ProductManagementArea.Products, ProductManagementArea.Versions)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.ReleasePackages.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var data = Data(context);

        var manifests = data.ReleasePackageComponents
            .GroupBy(c => c.PackageVersion, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var rows = data.ReleasePackages.Select(p => new ReleasePackageCsvRow
        {
            ImportId = p.Version,
            Version = p.Version,
            Name = p.Name,
            TargetDate = p.TargetDate,
            ReleasedDate = p.ReleasedDate,
        }).ToList();

        var batches = Batch(rows, r => r.ImportId);
        context.Log($"Importing {rows.Count} release packages and {data.ReleasePackageComponents.Count} manifest lines in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "release packages", batches, batch =>
        {
            var manifest = batch.SelectMany(p => manifests[p.ImportId]).Select(c => new ReleasePackageComponentCsvRow
            {
                PackageImportId = c.PackageVersion,
                ProductId = context.Id(ProductManagementArea.Products, c.ProductName),
                VersionNumber = c.VersionNumber,
                Kind = c.Kind,
            });

            return context.Client.ImportReleasePackages(CsvFile.ToBytes(batch), CsvFile.ToBytes(manifest), cancellationToken);
        });

        context.Publish(Name, created);
    }
}

/// <summary>
/// Loads the releases with their contents. Last of the records a release can carry, because a release is
/// only accepted as released once everything in it has shipped — which the import reads from what earlier
/// runs saved.
/// </summary>
public sealed class ReleasesArea() : ProductManagementSeedArea(
    ProductManagementArea.Releases, ProductManagementArea.Products, ProductManagementArea.Versions, ProductManagementArea.ReleasePackages)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.Releases.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var data = Data(context);

        var contents = data.ReleaseContents
            .GroupBy(c => c.ReleaseVersion, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var rows = data.Releases.Select(r => new ReleaseCsvRow
        {
            ImportId = r.Version,
            Version = r.Version,
            Name = r.Name,
            ProductId = r.ProductName is null ? null : context.Id(ProductManagementArea.Products, r.ProductName),
            TargetDate = r.TargetDate,
            ReleasedDate = r.ReleasedDate,
            Notes = r.Notes,
        }).ToList();

        var batches = Batch(rows, r => r.ImportId);
        context.Log($"Importing {rows.Count} releases and {data.ReleaseContents.Count} contents in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "releases", batches, batch =>
        {
            var contentRows = batch
                .SelectMany(r => contents.TryGetValue(r.ImportId, out var lines) ? lines : [])
                .Select(c => new ReleaseContentCsvRow
                {
                    ReleaseImportId = c.ReleaseVersion,
                    Kind = c.Kind,
                    PackageId = c.PackageVersion is null ? null : context.Id(ProductManagementArea.ReleasePackages, c.PackageVersion),
                    VersionId = c.VersionHandle is null ? null : context.Id(ProductManagementArea.Versions, c.VersionHandle),
                })
                .ToList();

            return context.Client.ImportReleases(
                CsvFile.ToBytes(batch),
                contentRows.Count > 0 ? CsvFile.ToBytes(contentRows) : null,
                cancellationToken);
        });

        context.Publish(Name, created);
    }
}

/// <summary>
/// Loads the deployment history. By far the largest file a seed writes, so it is batched; rows are
/// independent, and grouping by what was deployed only keeps one pipeline's history together.
/// </summary>
public sealed class DeploymentsArea() : ProductManagementSeedArea(
    ProductManagementArea.Deployments, ProductManagementArea.Environments, ProductManagementArea.Versions, ProductManagementArea.ReleasePackages)
{
    public override bool ShouldRun(SeedContext context) => context.ProductManagement?.Deployments.Count > 0;

    public override async Task Run(SeedContext context, CancellationToken cancellationToken)
    {
        var deployments = Data(context).Deployments;

        var rows = deployments.Select(d => new DeploymentCsvRow
        {
            ImportId = d.ImportId,
            VersionId = d.VersionHandle is null ? null : context.Id(ProductManagementArea.Versions, d.VersionHandle),
            PackageId = d.PackageVersion is null ? null : context.Id(ProductManagementArea.ReleasePackages, d.PackageVersion),
            EnvironmentName = d.EnvironmentName,
            ArtifactId = d.ArtifactId,
            StartedAt = DeploymentCsvRow.Timestamp(d.StartedAt),
            Outcome = d.Outcome,
            CompletedAt = d.CompletedAt is { } completed ? DeploymentCsvRow.Timestamp(completed) : null,
            RolledBackAt = d.RolledBackAt is { } rolledBack ? DeploymentCsvRow.Timestamp(rolledBack) : null,
            Reason = d.Reason,
        }).ToList();

        var batches = Batch(rows, r => (r.VersionId, r.PackageId));
        context.Log($"Importing {rows.Count} deployments in {batches.Count} batch(es)...");

        var created = await ImportBatches(context, "deployments", batches,
            batch => context.Client.ImportDeployments(CsvFile.ToBytes(batch), cancellationToken));

        context.Publish(Name, created);
    }
}
