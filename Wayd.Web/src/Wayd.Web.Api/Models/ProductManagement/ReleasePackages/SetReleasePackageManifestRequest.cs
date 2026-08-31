using Wayd.ProductManagement.Application.ReleasePackages.Commands;

namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// Replaces a package's manifest wholesale.
/// </summary>
/// <remarks>
/// Never incremental: a partially-updated manifest would claim a set of versions that never shipped
/// together.
/// </remarks>
public sealed record SetReleasePackageManifestRequest
{
    /// <summary>
    /// Every component version in this package. Replaces the manifest entirely.
    /// </summary>
    public List<ManifestEntryRequest> Components { get; set; } = [];

    public SetReleasePackageManifestCommand ToSetReleasePackageManifestCommand(Guid id) =>
        new(id, [.. Components.Select(c => c.ToManifestEntry())]);
}

public sealed class SetReleasePackageManifestRequestValidator
    : CustomValidator<SetReleasePackageManifestRequest>
{
    public SetReleasePackageManifestRequestValidator()
    {
        RuleFor(p => p.Components)
            .NotEmpty()
            .WithMessage("A package must be assembled from at least one component.");

        RuleForEach(p => p.Components)
            .SetValidator(new ManifestEntryRequestValidator());
    }
}
