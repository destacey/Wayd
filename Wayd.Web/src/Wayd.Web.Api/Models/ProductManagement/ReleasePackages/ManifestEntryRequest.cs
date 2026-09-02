using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// One component version in a package manifest.
/// </summary>
public sealed record ManifestEntryRequest
{
    /// <summary>
    /// The component this entry is for.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// The version record this line came from, where one exists. Optional: a package can record a
    /// component version that was never cut as a version of its own.
    /// </summary>
    /// <remarks>
    /// Named for the version it points at, matching <see cref="ManifestEntry.VersionId"/> and the
    /// column behind it. It was <c>ReleaseId</c> before Release and Version were split apart, which
    /// silently broke the link: JSON binds by name, so a client sending <c>versionId</c> left this
    /// null and the manifest recorded no version record at all.
    /// </remarks>
    public Guid? VersionId { get; set; }

    /// <summary>
    /// The component version. Free text, never parsed.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// Whether the component changed in this package or was carried forward unchanged.
    /// </summary>
    public ManifestEntryKind Kind { get; set; }

    public ManifestEntry ToManifestEntry() => new(ProductId, VersionId, Version, Kind);
}

public sealed class ManifestEntryRequestValidator : CustomValidator<ManifestEntryRequest>
{
    public ManifestEntryRequestValidator()
    {
        RuleFor(e => e.ProductId)
            .NotEmpty();

        RuleFor(e => e.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(e => e.Kind)
            .IsInEnum();
    }
}
