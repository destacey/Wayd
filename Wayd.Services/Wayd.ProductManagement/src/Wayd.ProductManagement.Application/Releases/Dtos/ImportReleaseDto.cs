namespace Wayd.ProductManagement.Application.Releases.Dtos;

/// <summary>
/// A single release row.
/// <para>
/// A release is identified by <see cref="Version"/> alone — deliberately not by product, even though
/// a release may name one. <see cref="ProductId"/> is nullable by design: a release spanning product
/// lines has no single owner, so a product-qualified key would be unresolvable for exactly the
/// releases the model exists to allow.
/// </para>
/// <para>
/// Contents arrive in a second file. They have to be a separate list because a release carries two
/// kinds — packages and directly-carried versions — and the rule that a version is announced once
/// spans both, so they are set together as one set.
/// </para>
/// </summary>
public sealed record ImportReleaseDto(
    string Version,
    string? Name,
    Guid? ProductId,
    LocalDate? TargetDate,
    LocalDate? ReleasedDate,
    long? Sequence,
    string? Notes,
    IReadOnlyList<ImportReleaseContentDto> Contents);

/// <summary>
/// One thing a release announces: either a package, or a version carried directly.
/// </summary>
/// <remarks>
/// Both are referenced by id. A version number is only unique within its product and a package version
/// carries no unique index, so neither label identifies a record on its own. The release this belongs
/// to is the row it was grouped onto, so it carries no reference of its own.
/// </remarks>
public sealed record ImportReleaseContentDto(
    ReleaseContentKind Kind,
    Guid? PackageId,
    Guid? VersionId);

/// <summary>Which of a release's two content routes a row describes.</summary>
public enum ReleaseContentKind
{
    /// <summary>A package the release shipped. The usual route.</summary>
    Package = 1,

    /// <summary>
    /// A version the release carries directly, for a single artifact that shipped on its own where
    /// nobody assembled a package.
    /// </summary>
    Version = 2,
}

public sealed class ImportReleaseDtoValidator : AbstractValidator<ImportReleaseDto>
{
    public ImportReleaseDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Version)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(r => r.Name)
            .MaximumLength(128);

        // No NotEmpty on Contents: an empty release is a legitimate state, not a draft — a
        // repackaging or a pricing change is announced with nothing deployed.
        RuleForEach(r => r.Contents)
            .NotNull()
            .SetValidator(new ImportReleaseContentDtoValidator());
    }
}

public sealed class ImportReleaseContentDtoValidator : AbstractValidator<ImportReleaseContentDto>
{
    public ImportReleaseContentDtoValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Kind)
            .IsInEnum();

        RuleFor(c => c.PackageId)
            .NotNull()
            .When(c => c.Kind == ReleaseContentKind.Package)
                .WithMessage("A package row must name a PackageId.");

        RuleFor(c => c.VersionId)
            .NotNull()
            .When(c => c.Kind == ReleaseContentKind.Version)
                .WithMessage("A version row must name a VersionId.");
    }
}
