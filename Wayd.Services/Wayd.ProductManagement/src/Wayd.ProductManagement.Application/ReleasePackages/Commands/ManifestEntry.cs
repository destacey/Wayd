using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.ReleasePackages.Commands;

/// <summary>
/// One component version in a package manifest.
/// </summary>
/// <param name="ProductId">The component this entry is for.</param>
/// <param name="VersionId">
/// The version record this line came from, where one exists. Optional, because a package can record a
/// version string that was never cut as a version of its own.
/// </param>
/// <param name="Version">The component version. Free text, never parsed.</param>
/// <param name="Kind">Whether the component changed here or was carried forward unchanged.</param>
public sealed record ManifestEntry(Guid ProductId, Guid? VersionId, string Version, ManifestEntryKind Kind);
