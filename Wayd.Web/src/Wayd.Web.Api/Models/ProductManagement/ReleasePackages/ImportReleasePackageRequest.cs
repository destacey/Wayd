using NodaTime.Extensions;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;

namespace Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

/// <summary>
/// A single CSV row for the release package import — one package, without its manifest.
/// <para>
/// A package is identified by <see cref="Version"/> alone: it has no product, because it spans them.
/// Manifest lines arrive in a second file and point back here by this row's <see cref="ImportId"/>.
/// </para>
/// </summary>
public sealed class ImportReleasePackageRequest
{
    /// <summary>
    /// The caller's own key for this row, unique within the file (case-insensitively). Results are
    /// reported against it, and the manifest file names it to say which package a line belongs to.
    /// Falls back to the row's position when the column is absent.
    /// </summary>
    public string? ImportId { get; set; }

    /// <summary>The package's own version, distinct from any component's. Free text, never parsed.</summary>
    public string Version { get; set; } = default!;

    public string? Name { get; set; }

    public DateTime? TargetDate { get; set; }

    /// <summary>When the package shipped. Supplying it makes the package Released.</summary>
    public DateTime? ReleasedDate { get; set; }

    public ImportReleasePackageDto ToImportReleasePackageDto(
        IReadOnlyList<ImportReleasePackageComponentDto> components) =>
        new(Version,
            Name,
            TargetDate?.ToLocalDateTime().Date,
            ReleasedDate?.ToLocalDateTime().Date,
            components);
}

public sealed class ImportReleasePackageRequestValidator : CustomValidator<ImportReleasePackageRequest>
{
    public ImportReleasePackageRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(p => p.Name)
            .MaximumLength(128);
    }
}

/// <summary>
/// A single CSV row for the manifest file: one component of one package.
/// </summary>
/// <remarks>
/// <see cref="VersionNumber"/> is text rather than a reference. Where a version record exists for that
/// product it is linked; where none does the string stands on its own, which is how a carried-forward
/// component — already running, never cut here — is recorded.
/// </remarks>
public sealed class ImportReleasePackageComponentRequest
{
    /// <summary>The `ImportId` of the package row this line belongs to.</summary>
    public string PackageImportId { get; set; } = default!;

    /// <summary>The component product, by id.</summary>
    public Guid ProductId { get; set; }

    /// <summary>The component's version in this package. Free text, never parsed.</summary>
    public string VersionNumber { get; set; } = default!;

    /// <summary>Whether the component changed in this package. `Changed` or `CarriedForward`.</summary>
    public string Kind { get; set; } = nameof(ManifestEntryKind.Changed);

    public ImportReleasePackageComponentDto ToImportReleasePackageComponentDto() =>
        new(ProductId, VersionNumber, Enum.Parse<ManifestEntryKind>(Kind.Trim(), ignoreCase: true));
}

public sealed class ImportReleasePackageComponentRequestValidator
    : CustomValidator<ImportReleasePackageComponentRequest>
{
    public ImportReleasePackageComponentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PackageImportId)
            .NotEmpty();

        RuleFor(c => c.ProductId)
            .NotEmpty();

        RuleFor(c => c.VersionNumber)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Kind)
            .NotEmpty()
            .Must(k => Enum.TryParse<ManifestEntryKind>(k.Trim(), ignoreCase: true, out _))
                .WithMessage("Kind must be either 'Changed' or 'CarriedForward'.");
    }
}
