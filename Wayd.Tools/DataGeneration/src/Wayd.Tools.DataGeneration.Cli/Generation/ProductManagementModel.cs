namespace Wayd.Tools.DataGeneration.Cli.Generation;

// The generated Product Management model, keyed by the generator's own handles — product names, version
// handles, package versions, environment names — for the same reason as the PPM model: ids exist only once
// the API has created something, so each seed area substitutes them as it writes its file.

/// <summary>A generated product. Its parent is named by <see cref="ParentName"/>, which is another product in the same set.</summary>
public sealed class ProductModel
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProductTypeName { get; init; }
    public string? ParentName { get; init; }
    public required string Status { get; init; }
    public string? Tags { get; init; }
}

/// <summary>
/// A generated version of the product named by <see cref="ProductName"/>. Its dates decide its status: none
/// is planned, a cut date is ready, a released date is released.
/// </summary>
public sealed class VersionModel
{
    public required string ProductName { get; init; }
    public required string Number { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? CutDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
    public string? Notes { get; init; }

    /// <summary>A version number is unique only within its product, so the handle carries both.</summary>
    public string Handle => HandleFor(ProductName, Number);

    public static string HandleFor(string productName, string number) => $"{productName}|{number}";
}

/// <summary>A generated release package, identified by its own <see cref="Version"/>.</summary>
public sealed class ReleasePackageModel
{
    public required string Version { get; init; }
    public string? Name { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
}

/// <summary>One manifest line of the package named by <see cref="PackageVersion"/>.</summary>
public sealed class ReleasePackageComponentModel
{
    public required string PackageVersion { get; init; }
    public required string ProductName { get; init; }
    public required string VersionNumber { get; init; }
    public required string Kind { get; init; }
}

/// <summary>A generated release — the announced thing — under the product line named by <see cref="ProductName"/>.</summary>
public sealed class ReleaseModel
{
    public required string Version { get; init; }
    public string? Name { get; init; }
    public string? ProductName { get; init; }
    public DateOnly? TargetDate { get; init; }
    public DateOnly? ReleasedDate { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// One thing the release named by <see cref="ReleaseVersion"/> announces: a package, named by its version,
/// or a version, named by its handle.
/// </summary>
public sealed class ReleaseContentModel
{
    public required string ReleaseVersion { get; init; }
    public required string Kind { get; init; }
    public string? PackageVersion { get; init; }
    public string? VersionHandle { get; init; }
}

/// <summary>A generated deployment environment. Names are unique across the organization.</summary>
public sealed class DeploymentEnvironmentModel
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required int RingOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// A generated deployment of exactly one of a version (by handle) or a package (by version). No outcome
/// means it is still in flight.
/// </summary>
public sealed class DeploymentModel
{
    public string? VersionHandle { get; init; }
    public string? PackageVersion { get; init; }
    public required string EnvironmentName { get; init; }
    public required string ArtifactId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public string? Outcome { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateTimeOffset? RolledBackAt { get; init; }
    public string? Reason { get; init; }

    /// <summary>
    /// Unique per row: a build reaches each environment once, and a retry is a new build.
    /// </summary>
    public string ImportId => $"{VersionHandle ?? PackageVersion}|{ArtifactId}|{EnvironmentName}";
}
