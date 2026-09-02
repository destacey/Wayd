namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One package shipped as part of a <see cref="Release"/>.
/// </summary>
/// <remarks>
/// A join rather than a <c>ReleaseId</c> on the package, because a package may serve more than one
/// release — the same weekly shipment can carry work announced under two product lines. Putting the
/// pointer on the package would force it to belong to exactly one, which is the shape rejected when
/// <c>Releases.PackageId</c> was dropped for the mirror-image reason.
/// </remarks>
public sealed class ReleasePackageInclusion : BaseAuditableEntity
{
    private ReleasePackageInclusion() { }

    internal ReleasePackageInclusion(Guid releaseId, Guid packageId)
    {
        ReleaseId = releaseId;
        PackageId = packageId;
    }

    /// <summary>The release announcing this package.</summary>
    public Guid ReleaseId { get; private init; }

    /// <summary>The package shipped.</summary>
    public Guid PackageId { get; private init; }

    /// <summary>The package shipped, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public ReleasePackage? Package { get; private init; }
}
