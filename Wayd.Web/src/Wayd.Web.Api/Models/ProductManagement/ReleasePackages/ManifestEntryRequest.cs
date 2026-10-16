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
    /// Named as the manifest entry's own <c>VersionId</c> and the column behind it are: JSON binds by name,
    /// so a property named anything else is left null and the manifest records no version.
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
