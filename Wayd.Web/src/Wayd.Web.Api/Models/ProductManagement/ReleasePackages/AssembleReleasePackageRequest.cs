using Wayd.ProductManagement.Application.ReleasePackages.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Assembles several component releases into one shipment.
/// </summary>
/// <remarks>
/// The manifest records every component version, changed and carried forward alike, so a reader can
/// reconstruct exactly what was in the box.
/// </remarks>
public sealed record AssembleReleasePackageRequest
{
    /// <summary>
    /// The package's own version, distinct from any component's. Free text, never parsed.
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// An optional human name for the package.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// When the package is expected to ship.
    /// </summary>
    public LocalDate? TargetDate { get; set; }

    /// <summary>
    /// Every component version in this package. A component may appear only once.
    /// </summary>
    public List<ManifestEntryRequest> Components { get; set; } = [];

    public AssembleReleasePackageCommand ToAssembleReleasePackageCommand() =>
        new(Version, Name, TargetDate, [.. Components.Select(c => c.ToManifestEntry())]);
}

public sealed class AssembleReleasePackageRequestValidator : CustomValidator<AssembleReleasePackageRequest>
{
    public AssembleReleasePackageRequestValidator()
    {
        RuleFor(p => p.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(p => p.Name)
            .MaximumLength(128);

        RuleFor(p => p.Components)
            .NotEmpty()
            .WithMessage("A package must be assembled from at least one component.");

        RuleForEach(p => p.Components)
            .SetValidator(new ManifestEntryRequestValidator());
    }
}
