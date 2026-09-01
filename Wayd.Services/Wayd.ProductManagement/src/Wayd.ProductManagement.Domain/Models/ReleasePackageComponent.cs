using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One line of a <see cref="ReleasePackage"/>'s manifest: a component product, the version of it the
/// package shipped, and whether that version was new in this package or carried forward unchanged.
/// </summary>
/// <remarks>
/// The version is captured here as text rather than only referenced through
/// <see cref="ReleaseId"/>, because a carried-forward component often has no release row in Wayd at
/// all — it was already running, and nobody cut anything for it. Recording the string is what lets the
/// manifest answer "what was running on this date" for every component rather than only the changed
/// ones.
/// </remarks>
public sealed class ReleasePackageComponent : BaseAuditableEntity
{
    private ReleasePackageComponent() { }

    internal ReleasePackageComponent(Guid packageId, Guid productId, Guid? releaseId, string version, ManifestEntryKind kind)
    {
        PackageId = packageId;
        ProductId = productId;
        ReleaseId = releaseId;
        Version = version;
        Kind = kind;
    }

    /// <summary>The package whose manifest this line belongs to.</summary>
    public Guid PackageId { get; private init; }

    /// <summary>The component product node this line describes.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>
    /// The product this line describes, when one is loaded.
    /// </summary>
    /// <remarks>
    /// For the read side only. No invariant depends on this being loaded.
    /// </remarks>
    public Product? Product { get; private init; }

    /// <summary>
    /// The release that supplied this version, where one is recorded in Wayd. Null for a
    /// carried-forward component whose version predates anything Wayd holds.
    /// </summary>
    public Guid? ReleaseId { get; private init; }

    /// <summary>The component's version in this package. Free text, never parsed.</summary>
    public string Version
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Version)).Trim();
    } = default!;

    /// <summary>Whether this component changed in this package or came along unchanged.</summary>
    public ManifestEntryKind Kind { get; private init; }
}
