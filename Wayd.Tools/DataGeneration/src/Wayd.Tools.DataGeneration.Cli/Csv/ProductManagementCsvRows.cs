namespace Wayd.Tools.DataGeneration.Cli.Csv;

// The Product Management CSV rows, as the API import endpoints consume them. Column names must match the
// request models in Wayd.Web.Api/Models/ProductManagement, and every column has to be present even when the
// seed leaves it empty: a missing header fails the whole file.

/// <summary>One row of the products CSV. The parent names another row in the same file by its ImportId.</summary>
public sealed class ProductCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string ProductTypeName { get; init; }
    public string? ParentImportId { get; init; }
    public string? ExternalId { get; init; }
    public string? Status { get; init; }

    /// <summary>Semicolon-separated <c>Category|Tag</c> pairs.</summary>
    public string? Tags { get; init; }
}

/// <summary>One row of the product dependencies CSV: <see cref="ProductId"/> relies on <see cref="DependsOnProductId"/>.</summary>
public sealed class ProductDependencyCsvRow
{
    public required string ImportId { get; init; }
    public required Guid ProductId { get; init; }
    public required Guid DependsOnProductId { get; init; }
    public required string Strength { get; init; }
    public string? Description { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
}

/// <summary>One row of the versions CSV. The dates decide the status.</summary>
public sealed class VersionCsvRow
{
    public required string ImportId { get; init; }
    public required Guid ProductId { get; init; }
    public required string Number { get; init; }
    public string? Name { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? CutDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
    public long? Sequence { get; init; }
    public string? Notes { get; init; }
}

/// <summary>One row of the release packages CSV, without its manifest.</summary>
public sealed class ReleasePackageCsvRow
{
    public required string ImportId { get; init; }
    public required string Version { get; init; }
    public string? Name { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
}

/// <summary>One manifest line, pointing back at its package row by that row's ImportId.</summary>
public sealed class ReleasePackageComponentCsvRow
{
    public required string PackageImportId { get; init; }
    public required Guid ProductId { get; init; }
    public required string VersionNumber { get; init; }
    public required string Kind { get; init; }
}

/// <summary>One row of the releases CSV, without its contents.</summary>
public sealed class ReleaseCsvRow
{
    public required string ImportId { get; init; }
    public required string Version { get; init; }
    public string? Name { get; init; }
    public Guid? ProductId { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
    public long? Sequence { get; init; }
    public string? Notes { get; init; }
}

/// <summary>One thing a release announces, pointing back at its release row by that row's ImportId.</summary>
public sealed class ReleaseContentCsvRow
{
    public required string ReleaseImportId { get; init; }
    public required string Kind { get; init; }
    public Guid? PackageId { get; init; }
    public Guid? VersionId { get; init; }
}

/// <summary>One row of the deployment environments CSV.</summary>
public sealed class DeploymentEnvironmentCsvRow
{
    public required string ImportId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int RingOrder { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// One row of the deployments CSV. Timestamps are text, matching the request model: the import refuses a
/// timestamp without an offset rather than read it in the server's zone.
/// </summary>
public sealed class DeploymentCsvRow
{
    public required string ImportId { get; init; }
    public Guid? VersionId { get; init; }
    public Guid? PackageId { get; init; }
    public required string EnvironmentName { get; init; }
    public string? ArtifactId { get; init; }
    public required string StartedAt { get; init; }
    public string? Outcome { get; init; }
    public string? CompletedAt { get; init; }
    public string? RolledBackAt { get; init; }
    public string? Reason { get; init; }

    /// <summary>ISO 8601 in UTC with an explicit <c>Z</c>, which the import reads as an instant.</summary>
    public static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
