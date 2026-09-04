using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.ReleasePackages.Dtos;

/// <summary>
/// A single release package row.
/// <para>
/// A package is identified by <see cref="Version"/> alone. It has no product — it spans them — so
/// there is nothing else to qualify it with, and its version is its only identifying field.
/// </para>
/// <para>
/// Like a version, the dates decide where it ends up: a row with no released date is assembled, and
/// one with a released date has shipped. A package is never "cut", so there is no middle step.
/// </para>
/// </summary>
public sealed record ImportReleasePackageDto(
    string Version,
    string? Name,
    LocalDate? TargetDate,
    LocalDate? ReleasedDate,
    IReadOnlyList<ImportReleasePackageComponentDto> Components);

/// <summary>
/// One manifest line: the component product, the version of it the package shipped, and whether that
/// version was new in this package or carried forward unchanged.
/// </summary>
/// <remarks>
/// <see cref="VersionNumber"/> is held as text because a carried-forward component often has no
/// version record in Wayd at all — it was already running and nobody cut anything for it. Where a
/// version record does match, the line is linked to it; where none does, the string stands on its own.
/// </remarks>
public sealed record ImportReleasePackageComponentDto(
    string PackageVersion,
    string ProductName,
    string VersionNumber,
    ManifestEntryKind Kind);

public sealed class ImportReleasePackageDtoValidator : AbstractValidator<ImportReleasePackageDto>
{
    public ImportReleasePackageDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(p => p.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(p => p.Name)
            .MaximumLength(128);

        // A package ships at least one component: an empty manifest says nothing about what shipped,
        // and the aggregate refuses one outright.
        RuleFor(p => p.Components)
            .NotEmpty()
                .WithMessage("A package must be assembled from at least one component.");

        RuleForEach(p => p.Components)
            .NotNull()
            .SetValidator(new ImportReleasePackageComponentDtoValidator());
    }
}

public sealed class ImportReleasePackageComponentDtoValidator
    : AbstractValidator<ImportReleasePackageComponentDto>
{
    public ImportReleasePackageComponentDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.PackageVersion)
            .NotEmpty();

        RuleFor(c => c.ProductName)
            .NotEmpty();

        RuleFor(c => c.VersionNumber)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(c => c.Kind)
            .IsInEnum();
    }
}
